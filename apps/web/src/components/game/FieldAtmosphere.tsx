"use client";

import { useEffect, useState, type CSSProperties } from "react";

type Star = {
  x: number;
  y: number;
  r: number;
  fill: string;
  o: number;
  twinkle: boolean;
  dur: string;
  delay: string;
};

function frac(seed: number) {
  const x = Math.sin(seed * 127.1 + 311.7) * 43758.5453;
  return x - Math.floor(x);
}

const STAR_COLORS = [
  "oklch(0.97 0.02 90)",
  "oklch(0.93 0.08 78)",
  "oklch(0.92 0.06 230)",
  "oklch(0.9 0.07 350)",
  "oklch(0.95 0.03 200)",
];

const STARS: Star[] = Array.from({ length: 140 }, (_, i) => {
  const n = i + 1;
  const roll = frac(n * 3.17);
  const giant = roll > 0.94;
  return {
    x: frac(n) * 100,
    y: frac(n + 40) * 100,
    r: giant ? 0.28 + frac(n + 90) * 0.18 : 0.045 + frac(n + 90) * 0.11,
    fill: STAR_COLORS[Math.floor(frac(n + 12) * STAR_COLORS.length)],
    o: 0.22 + frac(n + 7) * 0.7,
    twinkle: roll > 0.68,
    dur: `${3.6 + frac(n + 21) * 4.2}s`,
    delay: `${-frac(n + 5) * 6}s`,
  };
});

/** Full-viewport night-sky atmosphere behind the SVG board. */
export function FieldAtmosphere() {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  return (
    <div className="pathdle-atmosphere" aria-hidden>
      {mounted && (
        <svg
          className="pathdle-atmosphere__stars"
          viewBox="0 0 100 100"
          preserveAspectRatio="xMidYMid slice"
        >
          {STARS.map((star, i) => (
            <circle
              key={i}
              cx={star.x}
              cy={star.y}
              r={star.r}
              fill={star.fill}
              opacity={star.o}
              className={star.twinkle ? "pathdle-star-twinkle" : undefined}
              style={
                star.twinkle
                  ? ({
                      ["--twinkle-dur"]: star.dur,
                      ["--twinkle-delay"]: star.delay,
                      ["--star-o"]: String(star.o),
                    } as CSSProperties)
                  : undefined
              }
            />
          ))}
        </svg>
      )}
    </div>
  );
}
