using AlertDoors.Jobs;
namespace AlertDoors.Sources;
public static class LinkedInQueryBuilder
{
    public static Uri Build(SearchRequest request, int start = 0)
    {
        var mode = request.Mode switch { WorkMode.Remote => "2", WorkMode.Hybrid => "3", WorkMode.Onsite => "1", _ => "" };
        return new Uri("https://www.linkedin.com/jobs/search/?keywords=" + Uri.EscapeDataString(request.Keywords)
            + "&location=" + Uri.EscapeDataString(request.Location) + "&f_TPR=r86400&sortBy=DD&start=" + start
            + (mode.Length > 0 ? "&f_WT=" + mode : ""));
    }
}
