export function articleTitle(id: string, titles?: Record<string, string>) {
  if (titles?.[id]) return titles[id];
  return id.replaceAll("_", " ");
}
