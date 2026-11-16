namespace ClubPlatform.Core;

public record Geometry(double X, double Y, double Width, double Height, double Rotation);
public record MapElement(Guid Id, string Name, string Kind, string Mode, int Capacity, Geometry Geometry);
public class LayoutTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public int Revision { get; set; } = 1;
}
public class LayoutElement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VenueId { get; set; }
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Box";
    public string Mode { get; set; } = "Exclusive";
    public int Capacity { get; set; }
    public string GeometryJson { get; set; } = "{}";
}
public class LayoutVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VenueId { get; set; }
    public Guid TemplateId { get; set; }
    public int Number { get; set; }
    public int SourceRevision { get; set; }
    public string Name { get; set; } = "";
    public string ElementsJson { get; set; } = "[]";
    public DateTimeOffset PublishedAt { get; set; }
}
public class ClubEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public int Revision { get; set; } = 1;
    public int Capacity { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset EntryOpensAt { get; set; }
    public DateTimeOffset EntryClosesAt { get; set; }
    public DateTimeOffset RedemptionOpensAt { get; set; }
    public DateTimeOffset RedemptionClosesAt { get; set; }
    public Guid? LayoutVersionId { get; set; }
    public string MapJson { get; set; } = "[]";
    public string VenueName { get; set; } = "";
    public string TimeZone { get; set; } = "America/Lima";
}
public class EventResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VenueId { get; set; }
    public Guid EventId { get; set; }
    public Guid SourceElementId { get; set; }
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "";
    public int HumanCapacity { get; set; }
    public int AllocationLimit { get; set; }
}

public static class PlanningRules
{
    public static readonly string[] Kinds = ["Box", "Table", "General", "VIP", "Stage", "Bar", "Entrance", "Service"];
    public static readonly string[] Modes = ["Exclusive", "Capacity", "Decoration"];
    public static string? ValidateMap(IReadOnlyList<MapElement>? elements, int venueCapacity, bool publishing)
    {
        if (elements == null || elements.Count > 200 || elements.Any(x => x is null)) return "El mapa admite hasta 200 elementos.";
        if (elements.Select(x => x.Id).Distinct().Count() != elements.Count) return "Los IDs de elementos deben ser únicos.";
        foreach (var e in elements)
        {
            if (e.Id == Guid.Empty || string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 100 || !Kinds.Contains(e.Kind) || !Modes.Contains(e.Mode)) return "Elemento, nombre, tipo o modo inválido.";
            if (new[] { "Stage", "Bar", "Entrance", "Service" }.Contains(e.Kind) && e.Mode != "Decoration") return "Escenario, barra, acceso y servicios deben ser decorativos.";
            if (e.Capacity < 0 || e.Capacity > 100000 || (e.Mode == "Decoration" ? e.Capacity != 0 : e.Capacity == 0)) return "Los recursos requieren capacidad positiva y la decoración capacidad cero.";
            var g = e.Geometry;
            if (g == null || !new[] { g.X, g.Y, g.Width, g.Height, g.Rotation }.All(double.IsFinite) || g.X < 0 || g.Y < 0 || g.Width < .02 || g.Height < .02 || g.X + g.Width > 1.000001 || g.Y + g.Height > 1.000001 || g.Rotation < 0 || g.Rotation >= 360) return "La geometría debe usar coordenadas normalizadas dentro del mapa y rotación de 0 a 359 grados.";
        }
        var capacity = elements.Where(x => x.Mode != "Decoration").Sum(x => (long)x.Capacity);
        if (publishing && (capacity == 0 || capacity > venueCapacity)) return "La capacidad vendible del mapa debe estar entre 1 y el aforo de la sede.";
        return null;
    }
    public static string? ValidateEvent(ClubEvent e, int venueCapacity)
    {
        if (string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 160) return "El evento necesita un nombre de hasta 160 caracteres.";
        if (e.Capacity <= 0 || e.Capacity > venueCapacity) return "El aforo del evento debe ser positivo y no superar el de la sede.";
        if (e.StartsAt == default || e.StartsAt >= e.EndsAt) return "Inicio y fin deben incluir fechas completas y estar en orden.";
        if (e.EntryOpensAt == default || e.EntryOpensAt >= e.EntryClosesAt || e.EntryClosesAt < e.StartsAt || e.EntryClosesAt > e.EndsAt) return "La ventana de ingreso debe cerrar entre el inicio y fin del evento.";
        if (e.RedemptionOpensAt < e.StartsAt || e.RedemptionOpensAt >= e.RedemptionClosesAt || e.RedemptionClosesAt > e.EndsAt) return "La ventana de canje debe estar dentro del evento.";
        return null;
    }
}

