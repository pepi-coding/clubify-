import { createContext, useContext, useState, type ReactNode } from "react";
type Theme = "electric-night" | "midnight-lounge";
const ThemeContext = createContext<{
  theme: Theme;
  change: (theme: Theme) => void;
}>({ theme: "electric-night", change: () => {} });
export function NocheProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(() => {
    try {
      return localStorage.getItem("noche-theme") === "midnight-lounge"
        ? "midnight-lounge"
        : "electric-night";
    } catch {
      return "electric-night";
    }
  });
  return (
    <ThemeContext.Provider
      value={{
        theme,
        change: (value) => {
          setTheme(value);
          try {
            localStorage.setItem("noche-theme", value);
          } catch {
            /* The theme also works without storage. */
          }
        },
      }}
    >
      {children}
    </ThemeContext.Provider>
  );
}
export const useNoche = () => useContext(ThemeContext);
export function ThemePicker() {
  const { theme, change } = useNoche();
  return (
    <label className="n-theme">
      Estilo
      <select
        aria-label="Estilo visual"
        value={theme}
        onChange={(e) => change(e.target.value as Theme)}
      >
        <option value="electric-night">Electric Night</option>
        <option value="midnight-lounge">Midnight Lounge</option>
      </select>
    </label>
  );
}
export function NochePhoto({
  file,
  className = "",
  alt = "",
  loading,
}: {
  file: "hero" | "dj" | "combo";
  className?: string;
  alt?: string;
  loading?: "lazy";
}) {
  const { theme } = useNoche();
  return (
    <img
      className={className}
      src={`/assets/noche/${theme}/images/${file}.png`}
      alt={alt}
      loading={loading}
    />
  );
}
export const limaDay = (value: string) =>
  new Intl.DateTimeFormat("sv-SE", {
    timeZone: "America/Lima",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
