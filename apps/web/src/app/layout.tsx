import type { Metadata, Viewport } from "next";
import { Fraunces, Source_Sans_3 } from "next/font/google";
import "./globals.css";

const display = Fraunces({
  variable: "--font-display",
  subsets: ["latin"],
});

const ui = Source_Sans_3({
  variable: "--font-ui",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

const description =
  "Connect Wikipedia articles through a hidden constellation and reach today’s target in as few points as possible.";

const configuredSiteUrl =
  process.env.SITE_URL ??
  process.env.NEXT_PUBLIC_SITE_URL ??
  process.env.VERCEL_PROJECT_PRODUCTION_URL ??
  process.env.VERCEL_URL;

const metadataBase = new URL(
  configuredSiteUrl
    ? configuredSiteUrl.startsWith("http")
      ? configuredSiteUrl
      : `https://${configuredSiteUrl}`
    : "http://localhost:3000",
);

export const metadata: Metadata = {
  metadataBase,
  title: {
    default: "Pathdle — Daily Wikipedia Path Puzzle",
    template: "%s | Pathdle",
  },
  description,
  applicationName: "Pathdle",
  authors: [{ name: "Pathdle" }],
  creator: "Pathdle",
  publisher: "Pathdle",
  category: "games",
  keywords: [
    "daily puzzle",
    "Wikipedia game",
    "knowledge graph",
    "pathfinding game",
    "browser puzzle",
  ],
  alternates: {
    canonical: "/",
  },
  openGraph: {
    type: "website",
    locale: "en_US",
    url: "/",
    siteName: "Pathdle",
    title: "Pathdle — Chart the hidden path",
    description,
  },
  twitter: {
    card: "summary_large_image",
    title: "Pathdle — Chart the hidden path",
    description,
  },
  robots: {
    index: true,
    follow: true,
  },
  appleWebApp: {
    capable: true,
    title: "Pathdle",
    statusBarStyle: "black-translucent",
  },
};

export const viewport: Viewport = {
  colorScheme: "dark",
  themeColor: "#070811",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${display.variable} ${ui.variable} h-full antialiased`}
    >
      <body className="min-h-full font-[family-name:var(--font-ui)]">
        {children}
      </body>
    </html>
  );
}
