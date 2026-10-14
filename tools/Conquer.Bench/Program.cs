using System.Globalization;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

// Banco de pruebas de la población: una nación con una capital, unas ciudades y unas provincias vacías alrededor,
// sin rivales, simulada año a año con el juego de verdad. Sirve para ver cómo crece la gente y cómo migra antes de
// cambiar una regla, y para comparar después. Uso:
//
//   dotnet run -c Release --project tools/Conquer.Bench -- [opciones]
//
//   --years N        años que simular (40)
//   --cities N       ciudades además de la capital (2)
//   --provinces N    provincias sin ciudad (10)
//   --capital N      habitantes de la capital al empezar (los colonos iniciales)
//   --city N         habitantes de cada ciudad al empezar (1000)
//   --eras E:A,...   la nación entra en la era E en el año A (conoce todos los avances hasta ella),
//                    por ejemplo --eras Medieval:40,Industrial:80
//   --every N        cada cuántos años escribir una línea (5; los cinco primeros, siempre)

var spanish = (CultureInfo)CultureInfo.InvariantCulture.Clone();
spanish.NumberFormat.NumberDecimalSeparator = spanish.NumberFormat.PercentDecimalSeparator = ",";
spanish.NumberFormat.NumberGroupSeparator = spanish.NumberFormat.PercentGroupSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = spanish;

var options = new Dictionary<string, string>();
for (int i = 0; i + 1 < args.Length; i += 2) options[args[i].TrimStart('-')] = args[i + 1];
int Option(string name, int fallback) => options.TryGetValue(name, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;
int years = Option("years", 40), cityCount = Option("cities", 2), provinceCount = Option("provinces", 10), every = Option("every", 5);
int capitalPeople = Option("capital", GameRules.StartingCitizens), cityPeople = Option("city", 1000);
var eras = (options.GetValueOrDefault("eras") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(e => e.Split(':')).ToDictionary(e => int.Parse(e[1], CultureInfo.InvariantCulture), e => Enum.Parse<Era>(e[0]));

Console.WriteLine("Generando el mundo...");
var map = WorldGenerator.Generate(new WorldSettings(MapKind.Random, 42));
var s = GameSession.Create(map, 1, seed: 7, computerRivals: false);

// The capital where the starting settlers stand; the cities and provinces in rings of free land around it.
var settlers = s.Units.First(u => u.OwnerId == 0 && u.CanFoundCity);
var capital = map.Provinces[settlers.ProvinceId];
s.FoundCity(0, settlers.Id, "Capital");
capital.Population = capitalPeople;
var ring = new List<int>();
var seen = new HashSet<int> { capital.Id };
var queue = new Queue<int>([capital.Id]);
while (queue.Count > 0 && ring.Count < 10 * (cityCount + provinceCount))
    foreach (int n in map.Provinces[queue.Dequeue()].Neighbors)
        if (map.Provinces[n].IsClaimable && seen.Add(n)) { ring.Add(n); queue.Enqueue(n); }
var cities = new List<Province>();
foreach (var p in ring.Select(id => map.Provinces[id]))
{
    if (cities.Count == cityCount) break;
    if (p.Neighbors.Contains(capital.Id) || cities.Any(c => c.Neighbors.Contains(p.Id))) continue;
    s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, p.Id, 300).Id, $"Ciudad {cities.Count + 1}");
    p.Population = cityPeople;
    cities.Add(p);
}
var rural = ring.Select(id => map.Provinces[id]).Where(p => !cities.Contains(p)).Take(provinceCount).ToList();
foreach (var p in rural)
{
    var scouts = s.AddRegiment(0, p.Id, BattalionType.Scouts);
    s.Claim(0, scouts.Id);
    s.Disband(0, scouts.Id);
    p.Population = 0;
}
var towns = new List<Province> { capital }.Concat(cities).ToList();
double Fill(Province p) => p.Population / s.CapacityOf(p);
double Nation() => s.Human.Provinces.Sum(id => map.Provinces[id].Population);

Console.WriteLine($"Capital: capacidad {s.CapacityOf(capital):N0}; ciudades: {string.Join(", ", cities.Select(c => $"{s.CapacityOf(c):N0}"))}");
Console.WriteLine($"Provincias: {string.Join(", ", rural.Select(r => $"{s.CapacityOf(r):N0}"))}");
Console.WriteLine();

var departed = new HashSet<int>();
int totalSent = 0, wide = 0, receiversSum = 0;
int? lastMigration = null, allTen = null, allQuarter = null;
double before = Nation();
for (int year = 1; year <= years; year++)
{
    if (eras.TryGetValue(year - 1, out var era))
    {
        foreach (var tech in Techs.All.Where(t => t.Info().Era <= era)) s.Human.Learn(tech);
        Console.WriteLine($"-- año {year - 1}: la nación entra en la era {era.Name()} --");
    }
    int sent = 0;
    var receivers = new HashSet<int>();
    for (int h = 0; h < 24 * 365; h++)
    {
        s.Step();
        foreach (var m in s.Migrations.Where(m => m.OwnerId == 0 && departed.Add(m.Id)))
        {
            sent += m.People;
            receivers.Add(m.ToProvinceId);
        }
    }
    totalSent += sent;
    receiversSum += receivers.Count;
    if (receivers.Count >= 0.8 * rural.Count) wide++;
    if (sent > 0) lastMigration = year;
    var fills = rural.Select(Fill).ToList();
    if (fills.Min() >= 0.1) allTen ??= year;
    if (fills.Min() >= 0.25) allQuarter ??= year;
    double now = Nation(), growth = before > 0 ? now / before - 1 : 0;
    before = now;
    if (year <= 5 || year % every == 0 || year == years)
        Console.WriteLine($"año {year,3}: nación {now:N0} ({growth:+0.0%;-0.0%}); ciudades {string.Join(" / ", towns.Select(t => $"{t.Population:N0} ({Fill(t):P0})"))}; " +
                          $"campo {rural.Sum(q => q.Population):N0} ({fills.Min():P0}-{fills.Max():P0}); emigran {sent:N0} hacia {receivers.Count}/{rural.Count}");
}

Console.WriteLine();
Console.WriteLine($"Emigrados: {totalSent:N0}; destinos al año: {(double)receiversSum / years:0.0} de {rural.Count}; años con el 80 % de las provincias recibiendo: {wide}");
Console.WriteLine($"Todas las provincias pasan del 10 %: año {allTen?.ToString() ?? "-"}; del 25 %: año {allQuarter?.ToString() ?? "-"}; última migración: año {lastMigration?.ToString() ?? "-"}");
Console.WriteLine($"Urbana: {towns.Sum(t => t.Population) / Nation():P0} de la población");
Console.WriteLine($"Al final: nación {Nation():N0}; ciudades {string.Join(" / ", towns.Select(t => $"{Fill(t):P0}"))}; campo {rural.Select(Fill).Min():P0}-{rural.Select(Fill).Max():P0}");
