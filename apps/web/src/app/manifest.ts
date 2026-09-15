import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Pathdle — Daily Wikipedia Path Puzzle",
    short_name: "Pathdle",
    description:
      "Connect Wikipedia articles through a hidden constellation and reach today’s target in as few points as possible.",
    start_url: "/",
    scope: "/",
    display: "standalone",
    background_color: "#070811",
    theme_color: "#070811",
    categories: ["games", "education"],
    icons: [
      {
        src: "/icon.svg",
        sizes: "any",
        type: "image/svg+xml",
      },
    ],
  };
}
