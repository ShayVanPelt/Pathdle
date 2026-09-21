namespace Pathdle.Generator.Corpus;

/// <summary>
/// Builds Entity/Group/IN_GROUP/SHARES_GROUP corpus from Wikidata (or a local demo graph).
/// </summary>
internal sealed class CorpusIngestor(WikidataClient wikidata, Neo4jCorpusStore store)
{
    public async Task IngestAsync(
        string seedEntitiesPath,
        string corpusVersion,
        int maxEntities,
        CancellationToken ct,
        bool demoOnly = false)
    {
        await store.EnsureSchemaAsync(ct);
        await store.ClearCorpusAsync(ct);

        if (demoOnly)
        {
            await IngestDemoAsync(corpusVersion, ct);
            return;
        }

        var seedIds = await LoadSeedIdsAsync(seedEntitiesPath, ct);
        Console.WriteLine($"Seed entities: {seedIds.Count}");

        // Expand via SPARQL for a few allowlisted classes (bounded).
        var perClass = Math.Max(50, maxEntities / Math.Max(1, GroupVocabulary.AllowedInstanceClasses.Count));
        var discovered = new HashSet<string>(seedIds, StringComparer.Ordinal);
        foreach (var classId in GroupVocabulary.AllowedInstanceClasses.Take(12))
        {
            if (discovered.Count >= maxEntities) break;
            try
            {
                var found = await wikidata.SearchEntityIdsByInstanceOfAsync(classId, perClass, ct);
                foreach (var id in found)
                {
                    discovered.Add(id);
                    if (discovered.Count >= maxEntities) break;
                }

                Console.WriteLine($"  P31={classId}: +{found.Count} (total {discovered.Count})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  SPARQL skip {classId}: {ex.Message}");
            }
        }

        var take = discovered.Take(maxEntities).ToList();
        Console.WriteLine($"Fetching claims for {take.Count} entities…");
        var entities = await wikidata.GetEntitiesAsync(take, ct);

        // Collect group value labels.
        var valueIds = entities.Values
            .SelectMany(e => e.Memberships.Select(m => m.ValueId))
            .Distinct(StringComparer.Ordinal)
            .Where(v => !GroupVocabulary.DeniedValueIds.Contains(v))
            .ToList();
        Console.WriteLine($"Resolving {valueIds.Count} group value labels…");
        var labels = await wikidata.GetLabelsAsync(valueIds, ct);

        await BuildAndWriteAsync(
            corpusVersion,
            entities.Values.ToList(),
            labels,
            seedIds.ToHashSet(StringComparer.Ordinal),
            ct);
    }

    private async Task IngestDemoAsync(string corpusVersion, CancellationToken ct)
    {
        Console.WriteLine("Demo ingest: writing hand-authored Dell→Vitamin C group graph to Neo4j.");
        var seed = DemoCorpus.Build();
        await store.UpsertCorpusAsync(
            seed.Entities,
            seed.Groups,
            seed.Memberships,
            seed.ShareEdges,
            ct);
        await store.WriteCorpusMetaAsync(
            corpusVersion,
            seed.Entities.Count,
            seed.Groups.Count,
            seed.ShareEdges.Count,
            ct);
        Console.WriteLine(
            $"Demo corpus ready: {seed.Entities.Count} entities, {seed.Groups.Count} groups, {seed.ShareEdges.Count} edges.");
    }

    private async Task BuildAndWriteAsync(
        string corpusVersion,
        IReadOnlyList<WikidataEntity> entities,
        IReadOnlyDictionary<string, string> valueLabels,
        IReadOnlySet<string> seedIds,
        CancellationToken ct)
    {
        // Group → member set
        var groupMembers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var groupMeta = new Dictionary<string, (string Property, string ValueId)>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            // Fame gate: English Wikipedia + enough sitelinks (seeds always kept if labeled).
            var isSeed = seedIds.Contains(entity.Id);
            if (!isSeed
                && (!entity.HasEnWiki || entity.SitelinkCount < GroupVocabulary.MinSitelinks))
            {
                continue;
            }

            foreach (var (property, valueId) in entity.Memberships)
            {
                if (GroupVocabulary.DeniedValueIds.Contains(valueId)) continue;
                var gid = GroupVocabulary.GroupId(property, valueId);
                if (!groupMembers.TryGetValue(gid, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    groupMembers[gid] = set;
                    groupMeta[gid] = (property, valueId);
                }

                set.Add(entity.Id);
            }
        }

        var total = entities.Count;
        var eligibleGroups = new List<CorpusGroupWrite>();
        var eligibleGroupIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (gid, members) in groupMembers)
        {
            if (members.Count < GroupVocabulary.MinGroupSize
                || members.Count > GroupVocabulary.MaxGroupSize)
            {
                continue;
            }

            var (property, valueId) = groupMeta[gid];
            var label = valueLabels.TryGetValue(valueId, out var l) ? l : valueId;
            var frequency = (double)members.Count / total;
            var rarity = GroupVocabulary.Rarity(members.Count, total);
            eligibleGroups.Add(new CorpusGroupWrite(
                gid, label, property, valueId, frequency, rarity, members.Count));
            eligibleGroupIds.Add(gid);
        }

        // Keep entities with ≥2 eligible groups.
        var entityWrites = new List<CorpusEntityWrite>();
        var memberships = new List<(string EntityId, string GroupId)>();
        var entityEligibleGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            var isSeed = seedIds.Contains(entity.Id);
            if (!isSeed
                && (!entity.HasEnWiki || entity.SitelinkCount < GroupVocabulary.MinSitelinks))
            {
                continue;
            }

            var groups = entity.Memberships
                .Select(m => GroupVocabulary.GroupId(m.Property, m.ValueId))
                .Where(eligibleGroupIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (groups.Count < 2) continue;
            // Skip entities Wikidata returned without an English label (title == QID).
            if (!Generation.TitleQuality.HasPlayableTitle(entity.Title, entity.Id)) continue;

            // Popularity = Wikipedia sitelink count (fame). Seeds get a floor boost.
            var popularity = Math.Max(entity.SitelinkCount, isSeed ? GroupVocabulary.MinSitelinks : 0);
            if (isSeed) popularity = Math.Max(popularity, 40);

            entityWrites.Add(new CorpusEntityWrite(
                entity.Id,
                entity.Title,
                popularity,
                entity.Description,
                isSeed));
            entityEligibleGroups[entity.Id] = groups;
            foreach (var g in groups)
            {
                memberships.Add((entity.Id, g));
            }
        }

        var groupById = eligibleGroups.ToDictionary(g => g.Id, StringComparer.Ordinal);
        var shareEdges = new List<CorpusShareEdgeWrite>();
        var seenPairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (gid, members) in groupMembers)
        {
            if (!eligibleGroupIds.Contains(gid)) continue;
            if (!groupById.TryGetValue(gid, out var g)) continue;
            var list = members.Where(entityEligibleGroups.ContainsKey).OrderBy(x => x, StringComparer.Ordinal).ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    var key = CorpusGraph.UndirectedKey(a, b);
                    if (!seenPairs.Add(key))
                    {
                        // Keep highest-rarity group for the pair.
                        var existing = shareEdges.FindIndex(e =>
                            CorpusGraph.UndirectedKey(e.A, e.B) == key);
                        if (existing >= 0 && shareEdges[existing].Rarity < g.Rarity)
                        {
                            shareEdges[existing] = new CorpusShareEdgeWrite(
                                a, b, g.Id, g.Label, g.Rarity);
                        }

                        continue;
                    }

                    shareEdges.Add(new CorpusShareEdgeWrite(a, b, g.Id, g.Label, g.Rarity));
                }
            }
        }

        Console.WriteLine(
            $"Writing {entityWrites.Count} entities, {eligibleGroups.Count} groups, {shareEdges.Count} share edges…");

        await store.UpsertCorpusAsync(entityWrites, eligibleGroups, memberships, shareEdges, ct);
        await store.WriteCorpusMetaAsync(
            corpusVersion,
            entityWrites.Count,
            eligibleGroups.Count,
            shareEdges.Count,
            ct);

        Console.WriteLine("Ingest complete.");
    }

    private static async Task<List<string>> LoadSeedIdsAsync(string path, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct);
        var ids = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // Allow "Q312  # Apple Inc." — take only the leading QID token.
            var token = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
            var hash = token.IndexOf('#');
            if (hash >= 0) token = token[..hash];
            token = token.Trim();
            if (token.Length > 1
                && token[0] == 'Q'
                && token.Skip(1).All(char.IsDigit))
            {
                ids.Add(token);
            }
        }

        return ids.Distinct(StringComparer.Ordinal).ToList();
    }
}

/// <summary>Offline demo corpus mirroring the API hand-authored seed.</summary>
internal static class DemoCorpus
{
    public static (
        List<CorpusEntityWrite> Entities,
        List<CorpusGroupWrite> Groups,
        List<(string EntityId, string GroupId)> Memberships,
        List<CorpusShareEdgeWrite> ShareEdges) Build()
    {
        var entities = new[]
        {
            ("Dell", 8), ("Apple", 14), ("Orange", 10), ("Vitamin_C", 9),
            ("Microsoft", 12), ("Google", 11), ("Sony", 7),
            ("Banana", 6), ("Pear", 5), ("Lemon", 6), ("HP", 5), ("Intel", 7),
            ("iPhone", 8), ("Macintosh", 4),
            ("California", 9), ("Cupertino", 3), ("Citrus", 4),
            ("Ascorbic_acid", 2), ("Scurvy", 2),
            ("Kiwi_fruit", 5), ("Strawberry", 5), ("Samsung", 6),
            ("PlayStation", 5), ("Windows", 6),
            ("Android", 6), ("Steve_Jobs", 7), ("Tim_Cook", 4),
            ("Fruit_salad", 3), ("Orange_juice", 4),
            ("Broccoli", 3), ("Pepper", 3), ("Nintendo", 8)
        }.Select(t => new CorpusEntityWrite(t.Item1, t.Item1.Replace('_', ' '), t.Item2)).ToList();


        var groups = new List<CorpusGroupWrite>
        {
            G("industry:tech_company", "Technology company", "P452", "tech", 12),
            G("category:fruit", "Fruit", "P31", "fruit", 10),
            G("nutrient:vitamin_c", "Contains vitamin C", "P2868", "vitc", 8),
            G("botany:citrus", "Citrus fruit", "P279", "citrus", 4),
            G("maker:apple_inc", "Made by Apple", "P176", "apple", 4),
            G("org:apple_leadership", "Apple leadership", "P112", "apple_lead", 4),
            G("hq:cupertino", "Based in Cupertino", "P159", "cup", 3),
            G("place:california", "Located in California", "P131", "ca", 4),
            G("platform:os_vendor", "Operating system vendor", "P400", "os", 3),
            G("franchise:sony_gaming", "Sony gaming", "P179", "sony", 3),
            G("category:game_console", "Game console maker", "P31", "console", 4),
            G("platform:android_devices", "Android devices", "P400", "android", 3),
            G("dish:fruit_salad", "Fruit salad ingredient", "P186", "salad", 4),
            G("product:orange_juice", "Orange juice", "P186", "oj", 3),
            G("chem:ascorbic", "Also known as", "P460", "asc", 3),
            G("medicine:scurvy", "Prevents scurvy", "P769", "scurvy", 3),
            G("category:vegetable", "Vegetable", "P31", "veg", 3),
        };

        // Membership + share edges derived from the same hand seed relationships.
        var share = new List<(string A, string B, string G)>
        {
            ("Dell", "Apple", "industry:tech_company"),
            ("Apple", "Orange", "category:fruit"),
            ("Orange", "Vitamin_C", "nutrient:vitamin_c"),
            ("Dell", "Microsoft", "industry:tech_company"),
            ("Dell", "HP", "industry:tech_company"),
            ("Apple", "Microsoft", "industry:tech_company"),
            ("Apple", "Google", "industry:tech_company"),
            ("Apple", "Sony", "industry:tech_company"),
            ("Microsoft", "Google", "industry:tech_company"),
            ("Microsoft", "Sony", "industry:tech_company"),
            ("Google", "Samsung", "industry:tech_company"),
            ("Sony", "Samsung", "industry:tech_company"),
            ("HP", "Intel", "industry:tech_company"),
            ("Intel", "Microsoft", "industry:tech_company"),
            ("Apple", "Banana", "category:fruit"),
            ("Apple", "Pear", "category:fruit"),
            ("Orange", "Banana", "category:fruit"),
            ("Orange", "Pear", "category:fruit"),
            ("Orange", "Lemon", "category:fruit"),
            ("Banana", "Pear", "category:fruit"),
            ("Banana", "Kiwi_fruit", "category:fruit"),
            ("Pear", "Kiwi_fruit", "category:fruit"),
            ("Lemon", "Kiwi_fruit", "category:fruit"),
            ("Kiwi_fruit", "Strawberry", "category:fruit"),
            ("Strawberry", "Orange", "category:fruit"),
            ("Lemon", "Vitamin_C", "nutrient:vitamin_c"),
            ("Kiwi_fruit", "Vitamin_C", "nutrient:vitamin_c"),
            ("Strawberry", "Vitamin_C", "nutrient:vitamin_c"),
            ("Broccoli", "Vitamin_C", "nutrient:vitamin_c"),
            ("Pepper", "Vitamin_C", "nutrient:vitamin_c"),
            ("Orange_juice", "Vitamin_C", "nutrient:vitamin_c"),
            ("Orange", "Lemon", "botany:citrus"),
            ("Orange", "Citrus", "botany:citrus"),
            ("Lemon", "Citrus", "botany:citrus"),
            ("Apple", "iPhone", "maker:apple_inc"),
            ("Apple", "Macintosh", "maker:apple_inc"),
            ("iPhone", "Macintosh", "maker:apple_inc"),
            ("Apple", "Steve_Jobs", "org:apple_leadership"),
            ("Apple", "Tim_Cook", "org:apple_leadership"),
            ("Steve_Jobs", "Tim_Cook", "org:apple_leadership"),
            ("Apple", "Cupertino", "hq:cupertino"),
            ("Cupertino", "California", "place:california"),
            ("Google", "California", "place:california"),
            ("Microsoft", "Windows", "platform:os_vendor"),
            ("Google", "Android", "platform:os_vendor"),
            ("Sony", "PlayStation", "franchise:sony_gaming"),
            ("Nintendo", "PlayStation", "category:game_console"),
            ("Sony", "Nintendo", "category:game_console"),
            ("Samsung", "Android", "platform:android_devices"),
            ("Banana", "Fruit_salad", "dish:fruit_salad"),
            ("Orange", "Fruit_salad", "dish:fruit_salad"),
            ("Strawberry", "Fruit_salad", "dish:fruit_salad"),
            ("Orange", "Orange_juice", "product:orange_juice"),
            ("Ascorbic_acid", "Vitamin_C", "chem:ascorbic"),
            ("Scurvy", "Vitamin_C", "medicine:scurvy"),
            ("Broccoli", "Pepper", "category:vegetable"),
        };

        var groupLookup = groups.ToDictionary(g => g.Id, StringComparer.Ordinal);
        var memberships = new HashSet<(string, string)>();
        var shareEdges = new List<CorpusShareEdgeWrite>();
        foreach (var (a, b, gid) in share)
        {
            var g = groupLookup[gid];
            memberships.Add((a, gid));
            memberships.Add((b, gid));
            shareEdges.Add(new CorpusShareEdgeWrite(a, b, g.Id, g.Label, g.Rarity));
        }

        return (entities, groups, memberships.ToList(), shareEdges);

        static CorpusGroupWrite G(string id, string label, string prop, string valueId, int members)
        {
            var freq = members / 32.0;
            var rarity = GroupVocabulary.Rarity(members, 32);
            return new CorpusGroupWrite(id, label, prop, valueId, freq, rarity, members);
        }
    }
}
