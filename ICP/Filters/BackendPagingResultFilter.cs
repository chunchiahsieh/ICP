using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ICP.Filters;

/// <summary>
/// Applies the standard ProDataTables page contract to partial-list responses.
/// Query actions still own their filtering and authorization; this filter ensures
/// that only the requested page is rendered and exposes the total through headers.
/// </summary>
public sealed class BackendPagingResultFilter : IAsyncResultFilter
{
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 100;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is PartialViewResult partialView &&
            context.HttpContext.Request.HasFormContentType)
        {
            var form = await context.HttpContext.Request.ReadFormAsync(context.HttpContext.RequestAborted);
            if (form.TryGetValue("Page", out var requestedPageValue) || form.ContainsKey("PageSize"))
            {
                var page = ParsePositiveInt(requestedPageValue.FirstOrDefault(), 1);
                var pageSize = Math.Min(ParsePositiveInt(form["PageSize"].FirstOrDefault(), DefaultPageSize), MaximumPageSize);
                var list = FindList(partialView.Model);

                if (list is not null)
                {
                    var totalCount = list.Count;
                    var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                    page = Math.Min(page, totalPages);
                    var skip = (page - 1) * pageSize;

                    for (var index = list.Count - 1; index >= 0; index--)
                    {
                        if (index < skip || index >= skip + pageSize)
                        {
                            list.RemoveAt(index);
                        }
                    }

                    var headers = context.HttpContext.Response.Headers;
                    headers["X-ICP-Total-Count"] = totalCount.ToString();
                    headers["X-ICP-Page"] = page.ToString();
                    headers["X-ICP-Page-Size"] = pageSize.ToString();
                }
            }
        }

        await next();
    }

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var number) && number > 0 ? number : fallback;

    private static IList? FindList(object? model)
    {
        if (model is null)
        {
            return null;
        }

        foreach (var propertyName in new[] { "ListData", "Items" })
        {
            var value = model.GetType().GetProperty(propertyName)?.GetValue(model);
            if (value is IList list && !list.IsReadOnly)
            {
                return list;
            }
        }

        return null;
    }
}
