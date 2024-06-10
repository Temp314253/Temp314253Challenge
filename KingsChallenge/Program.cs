using System.Collections.Immutable;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

using var serviceProvider = new ServiceCollection()
    .AddHttpClient<KingsApi>(httpClient => { httpClient.BaseAddress = new Uri("https://gist.githubusercontent.com/"); }).Services
    .AddSingleton<KingsStatisticsService>()
    .AddSingleton(TimeProvider.System)
    .BuildServiceProvider();

var kingsStatisticsService = serviceProvider.GetRequiredService<KingsStatisticsService>();
var kingsStatistics = await kingsStatisticsService.GetKingsStatistics();

Console.WriteLine("1. How many monarchs are there in the list?");
Console.WriteLine(kingsStatistics.KingsCount);
Console.WriteLine("2. Which monarch ruled the longest (and for how long)?");
Console.WriteLine($"Name: {kingsStatistics.MonarchThatRuledTheLongest.Name} Years: {kingsStatistics.MonarchThatRuledTheLongest.RuleYears}");
Console.WriteLine("3. Which house ruled the longest (and for how long)?");
Console.WriteLine($"Name: {kingsStatistics.HouseThatRuledTheLongest.Name} Years: {kingsStatistics.HouseThatRuledTheLongest.RuleYears}");
Console.WriteLine("4. What was the most common first name?");
Console.WriteLine(kingsStatistics.TheMostCommonFirstName);

/*
1. How many monarchs are there in the list?
57
2. Which monarch ruled the longest (and for how long)?
Name: Elizabeth II Years: 72
3. Which house ruled the longest (and for how long)?
Name: House of Hanover Years: 187
4. What was the most common first name?
Edward
*/ 

public record King(
    int Id, 
    string FirstName, 
    string FullName, 
    (int StartYear, int? EndYear) Rule, /* End year can be null when king is alive */
    string House);

public record KingsStatistics(
    int KingsCount, 
    (string Name, int RuleYears) MonarchThatRuledTheLongest, 
    (string Name, int RuleYears) HouseThatRuledTheLongest, 
    string TheMostCommonFirstName);

public class KingsStatisticsService(KingsApi kingsApi, TimeProvider timeProvider)
{
    /* To keep things simple (only one winner) results are ordered by years and then alphabetically */
    public async Task<KingsStatistics> GetKingsStatistics()
    {
        var kings = await kingsApi.GetKings();
        return new KingsStatistics(
            KingsCount: kings.Count(), 
            MonarchThatRuledTheLongest: 
                kings.OrderByDescending(RuleLength)
                .ThenBy(king => king.FullName)
                .Select(king => (king.FullName, RuleLength(king)))
                .First(),
            HouseThatRuledTheLongest:
                kings.GroupBy(king => king.House)
                    .Select(houseGroup => (houseName: houseGroup.Key, ruleLength: houseGroup.Sum(RuleLength)))
                    .OrderByDescending(house => house.ruleLength)
                    .ThenBy(house => house.houseName)
                    .First(),
            TheMostCommonFirstName: 
                kings.GroupBy(king => king.FirstName)
                .OrderByDescending(nameGroup => nameGroup.Count())
                .ThenBy(nameGroup => nameGroup.Key)
                .Select(nameGroup => nameGroup.Key)
                .First());
    }

    private int RuleLength(King king) => (king.Rule.EndYear ?? timeProvider.GetUtcNow().Year) - king.Rule.StartYear;
}

public class KingsApi(HttpClient httpClient)
{
    public async Task<ImmutableList<King>> GetKings() => 
        [.. (await httpClient.GetFromJsonAsync<KingDTO[]>("christianpanton/10d65ccef9f29de3acd49d97ed423736/raw/b09563bc0c4b318132c7a738e679d4f984ef0048/kings"))!
        .Select(kingDto => new King(kingDto.id, FirstName(kingDto.nm), kingDto.nm, ParseYears(kingDto.yrs), kingDto.hse))];

    private record KingDTO(int id, string nm, string cty, string hse, string yrs);

    private static string FirstName(string fullName) => fullName.Split(" ")[0];

    private static (int StartYear, int? EndYear) ParseYears(string years)
    {
        var splittedYears = years.Split("-");

        /* King ruled 1 year */
        if(splittedYears.Length == 1)
            return (int.Parse(splittedYears[0]), int.Parse(splittedYears[0]));

        if(splittedYears.Length == 2 && splittedYears[1] != string.Empty)
            return (int.Parse(splittedYears[0]), int.Parse(splittedYears[1]));

        /* End year can be null when king is alive */
        return (int.Parse(splittedYears[0]), null);
    }
}