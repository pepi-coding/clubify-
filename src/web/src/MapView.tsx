import { useRef } from "react";
import type { components } from "./api/schema";
export type MapElement = components["schemas"]["MapElement"];
const labels: Record<string, string> = {
  Box: "Box",
  Table: "Mesa",
  General: "General",
  VIP: "VIP",
  Stage: "Escenario",
  Bar: "Barra",
  Entrance: "Acceso",
  Service: "Servicio",
};
export const modeName: Record<string, string> = {
  Exclusive: "Exclusivo",
  Capacity: "Por cupo",
  Decoration: "Decorativo",
};
export function MapCanvas({
  elements,
  selected,
  onSelect,
  onChange,
  availability,
}: {
  elements: MapElement[];
  availability?: Record<string, { state: string; label: string }>;
  selected?: string;
  onSelect?: (id: string) => void;
  onChange?: (elements: MapElement[]) => void;
}) {
  const drag = useRef<{
    id: string;
    startX: number;
    startY: number;
    x: number;
    y: number;
  } | null>(null);
  return (
    <svg
      className={"map-canvas " + (onChange ? "editable" : "")}
      viewBox="0 0 1000 700"
      role="img"
      aria-label={
        onChange
          ? "Editor de distribución de sede"
          : "Mapa publicado del evento"
      }
      onPointerMove={(e) => {
        if (!drag.current || !onChange) return;
        const rect = e.currentTarget.getBoundingClientRect();
        const d = drag.current;
        onChange(
          elements.map((item) =>
            item.id !== d.id
              ? item
              : {
                  ...item,
                  geometry: {
                    ...item.geometry,
                    x: Math.max(
                      0,
                      Math.min(
                        1 - item.geometry.width,
                        d.x + (e.clientX - d.startX) / rect.width,
                      ),
                    ),
                    y: Math.max(
                      0,
                      Math.min(
                        1 - item.geometry.height,
                        d.y + (e.clientY - d.startY) / rect.height,
                      ),
                    ),
                  },
                },
          ),
        );
      }}
      onPointerUp={() => {
        drag.current = null;
      }}
      onPointerCancel={() => {
        drag.current = null;
      }}
    >
      <defs>
        <pattern
          id="map-grid"
          width="50"
          height="50"
          patternUnits="userSpaceOnUse"
        >
          <path
            d="M 50 0 L 0 0 0 50"
            fill="none"
            stroke="#303344"
            strokeWidth="1"
          />
        </pattern>
      </defs>
      <rect width="1000" height="700" rx="12" fill="#131722" />
      <rect width="1000" height="700" fill="url(#map-grid)" />
      {elements.map((item) => {
        const g = item.geometry;
        const stock = availability?.[item.id];
        const x = g.x * 1000,
          y = g.y * 700,
          w = g.width * 1000,
          h = g.height * 700;
        return (
          <g
            key={item.id}
            data-element-id={item.id}
            transform={`rotate(${g.rotation} ${x + w / 2} ${y + h / 2})`}
            tabIndex={onSelect ? 0 : undefined}
            role={onSelect ? "button" : undefined}
            aria-label={`${item.name}, ${modeName[item.mode]}, ${item.capacity} personas${stock ? ", " + stock.label : ""}`}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onSelect?.(item.id);
              }
            }}
            onPointerDown={(e) => {
              if (!onSelect) return;
              e.preventDefault();
              onSelect(item.id);
              e.currentTarget.ownerSVGElement?.setPointerCapture(e.pointerId);
              drag.current = {
                id: item.id,
                startX: e.clientX,
                startY: e.clientY,
                x: g.x,
                y: g.y,
              };
            }}
          >
            <rect
              x={x}
              y={y}
              width={w}
              height={h}
              rx="9"
              fill={
                stock
                  ? ({
                      Available: "#214b54",
                      Reserved: "#775b1a",
                      Sold: "#633344",
                      Blocked: "#464653",
                    }[stock.state] ?? "#214b54")
                  : item.mode === "Decoration"
                    ? "#303847"
                    : item.mode === "Exclusive"
                      ? "#453562"
                      : "#214b54"
              }
              stroke={selected === item.id ? "#f7dd8e" : "#8b91b3"}
              strokeWidth={selected === item.id ? 4 : 2}
            />
            <text
              x={x + w / 2}
              y={y + h / 2 - 3}
              textAnchor="middle"
              fill="#fff"
              fontSize="17"
              pointerEvents="none"
            >
              {item.name.length > 18 ? item.name.slice(0, 17) + "…" : item.name}
            </text>
            <text
              x={x + w / 2}
              y={y + h / 2 + 20}
              textAnchor="middle"
              fill="#d1d9e9"
              fontSize="13"
              pointerEvents="none"
            >
              {item.mode === "Decoration"
                ? labels[item.kind]
                : stock
                  ? stock.label
                  : `${item.capacity} personas`}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
export function MapList({ elements }: { elements: MapElement[] }) {
  return (
    <div className="map-list" aria-label="Lista de elementos">
      {elements.length === 0 ? (
        <p>Este mapa no tiene elementos.</p>
      ) : (
        elements.map((e) => (
          <div className="list-row" key={e.id}>
            <strong>{e.name}</strong>
            <span>
              {labels[e.kind]} · {modeName[e.mode]}
              {e.mode !== "Decoration"
                ? ` · ${e.capacity} personas`
                : " · Sin venta"}
            </span>
          </div>
        ))
      )}
    </div>
  );
}
export function ElementProperties({
  element,
  onChange,
  onDelete,
}: {
  element: MapElement;
  onChange: (e: MapElement) => void;
  onDelete: () => void;
}) {
  const update = (name: string, value: string | number) =>
    onChange({ ...element, [name]: value });
  return (
    <aside className="properties">
      <h3>Propiedades del elemento</h3>
      <label>
        Nombre del elemento
        <input
          value={element.name}
          maxLength={100}
          onChange={(e) => update("name", e.target.value)}
        />
      </label>
      <label>
        Modo de venta
        <select
          value={element.mode}
          disabled={["Stage", "Bar", "Entrance", "Service"].includes(
            element.kind,
          )}
          onChange={(e) =>
            onChange({
              ...element,
              mode: e.target.value,
              capacity:
                e.target.value === "Decoration"
                  ? 0
                  : Math.max(1, element.capacity),
            })
          }
        >
          {Object.entries(modeName).map(([v, n]) => (
            <option value={v} key={v}>
              {n}
            </option>
          ))}
        </select>
      </label>
      <label>
        Capacidad del elemento
        <input
          type="number"
          min={element.mode === "Decoration" ? 0 : 1}
          max={100000}
          value={element.capacity}
          disabled={element.mode === "Decoration"}
          onChange={(e) => update("capacity", Number(e.target.value))}
        />
      </label>
      {(["x", "y", "width", "height", "rotation"] as const).map((key) => (
        <label key={key}>
          {
            {
              x: "Posición X (%)",
              y: "Posición Y (%)",
              width: "Ancho (%)",
              height: "Alto (%)",
              rotation: "Rotación (grados)",
            }[key]
          }
          <input
            type="number"
            min={key === "width" || key === "height" ? 2 : 0}
            max={key === "rotation" ? 359 : 100}
            step={key === "rotation" ? 1 : 0.1}
            value={
              Math.round(
                element.geometry[key] * (key === "rotation" ? 1 : 100) * 10,
              ) / 10
            }
            onChange={(e) => {
              const next = {
                ...element.geometry,
                [key]: Number(e.target.value) / (key === "rotation" ? 1 : 100),
              };
              if (key === "width")
                next.width = Math.min(next.width, 1 - next.x);
              if (key === "height")
                next.height = Math.min(next.height, 1 - next.y);
              if (key === "x") next.x = Math.min(next.x, 1 - next.width);
              if (key === "y") next.y = Math.min(next.y, 1 - next.height);
              onChange({ ...element, geometry: next });
            }}
          />
        </label>
      ))}
      <button type="button" className="danger" onClick={onDelete}>
        Eliminar elemento
      </button>
    </aside>
  );
}
export const palette = Object.entries(labels);
