using System.Reflection;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace UnlockExtractions;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.boombah.unlockextractions";
    public string Name { get; init; } = "UnlockExtractions";
    public string Author { get; init; } = "Boombah";
    public List<string>? Contributors { get; init; }
    public Version Version { get; init; } = new("1.0.0");
    public Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/SattlerGabriel/Unlock-Extractions";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

public record ModConfig
{
    public bool EnableAllExits { get; init; } = true;
    public bool PaidCoopExits { get; init; } = true;
    public string Currency { get; init; } = "Roubles";
    public int Price { get; init; } = 25000;
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class UnlockExtractions(LocationTable locationTable, ISptLogger<UnlockExtractions> logger) : IOnLoad
{
    private static readonly Dictionary<string, MongoId> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Roubles"] = Money.ROUBLES,
        ["Dollars"] = Money.DOLLARS,
        ["Euros"] = Money.EUROS,
        ["GP"] = Money.GP
    };

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "config.json");
        var config = File.Exists(path)
            ? JsonSerializer.Deserialize<ModConfig>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ModConfig()
            : new ModConfig();

        // Anything not in the table is treated as a raw item template id
        string? currencyTpl = Currencies.TryGetValue(config.Currency, out var tpl) ? tpl.ToString()
            : MongoId.IsValidMongoId(config.Currency) ? config.Currency : null;
        var paidCoop = config.PaidCoopExits;
        if (paidCoop && (currencyTpl is null || config.Price <= 0))
        {
            logger.Error($"[UnlockExtractions] Invalid Currency '{config.Currency}' or Price {config.Price}; co-op exits left unchanged");
            paidCoop = false;
        }

        int unlocked = 0, converted = 0;
        foreach (var location in locationTable.GetDictionary().Values)
        {
            var exits = location.Base?.Exits?.ToList();
            if (exits is null || exits.Count == 0)
            {
                continue;
            }

            // Client only offers exits whose EntryPoints contain your spawn zone, so list every zone on every exit
            var allEntryPoints = string.Join(",", exits
                .SelectMany(e => (e.EntryPoints ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase));

            foreach (var exit in exits)
            {
                if (config.EnableAllExits)
                {
                    exit.Chance = 100;
                    exit.ChancePVE = 100;
                    exit.EntryPoints = allEntryPoints;
                    unlocked++;
                }

                if (!paidCoop ||
                    exit.PassageRequirement != RequirementState.ScavCooperation)
                    continue;
                
                exit.PassageRequirement = RequirementState.TransferItem;
                exit.ExfiltrationType = ExfiltrationType.SharedTimer;
                exit.Id = currencyTpl;
                exit.Count = config.Price;
                exit.CountPVE = config.Price;
                exit.RequirementTip = "EXFIL_Item";
                converted++;
            }
        }

        logger.Success($"[UnlockExtractions] Unlocked {unlocked} exits, converted {converted} co-op exits to {config.Price} {config.Currency}");
        return Task.CompletedTask;
    }
}
