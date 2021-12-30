using System.Collections.Generic;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;

public static class HttpRequestDataExtensions
{
    /// <summary>
    /// Create an HttpStatus No Content based response.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The <see cref="HttpStatusCode.NoContent"/> based response.</returns>
    public static HttpResponseData NoContentResponse(this HttpRequestData request) => request.CreateResponse(HttpStatusCode.NoContent);

    /// <summary>
    /// Creates an Ok Response with string based content. This response has a text/plain content type.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="content">The string content to add to the response.</param>
    /// <returns>The <see cref="HttpStatusCode.OK"/> based response.</returns>
    public static async Task<HttpResponseData> OkResponse(this HttpRequestData request, string content)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync(content).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Creates an Ok Response with object based content.
    /// </summary>
    /// <typeparam name="T">The type of the <param name="data"></param>.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="data">The data payload to serialize into the body of the response.</param>
    /// <returns>The <see cref="HttpStatusCode.OK"/> based response.</returns>
    public static async Task<HttpResponseData> OkObjectResponse<T>(this HttpRequestData request, T data)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(data).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Creates a not found based response.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    public static HttpResponseData NotFoundResponse(this HttpRequestData request) => request.CreateResponse(HttpStatusCode.NotFound);

    /// <summary>
    /// Created at response which returns the routed information in the Location header
    /// </summary>
    /// <typeparam name="T">The type of the <param name="data"></param>.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="nameOfFunction">The name of the function for the target route.</param>
    /// <param name="routeValues">The route values to use against the route on the <see cref="nameOfFunction"/> named function.</param>
    /// <param name="data">The optional data payload to serialize into the body of the response.</param>
    /// <returns></returns>
    public static async Task<HttpResponseData> CreatedAtResponse<T>(this HttpRequestData request, string nameOfFunction, object routeValues, T data = default)
    {
        var methods = typeof(Functions).GetMethods();
        var functions = methods.Where(x => x.GetCustomAttribute<FunctionAttribute>() != null).ToList();

        var root = functions.FirstOrDefault(x => x.Name == nameOfFunction);

        var methodParameters = root.GetParameters().FirstOrDefault(x => x.GetCustomAttribute<HttpTriggerAttribute>() != null);

        var routeInfo = methodParameters?.GetCustomAttribute<HttpTriggerAttribute>()?.Route;

        var template = TemplateParser.Parse(routeInfo);

        var values = new RouteValueDictionary(routeValues);

        // get the api prefix from configuration somehow!?
        var urlBits = new List<object>();
        urlBits.Add(string.Empty); // HACK: additional item so the join below starts with a slash
        urlBits.Add("api"); // HACK: this needs to come from hosting configuration some how

        // now we have the template parsed need to push it together with an anonymous object
        foreach (var segment in template.Segments)
        {
            foreach (var templatePart in segment.Parts)
            {
                if (!string.IsNullOrWhiteSpace(templatePart.Text))
                {
                    urlBits.Add(templatePart.Text);
                }

                if (!string.IsNullOrWhiteSpace(templatePart.Name)
                    && values.TryGetValue(templatePart.Name, out var value))
                {
                    urlBits.Add(value);
                }
            }
        }

        var url = string.Join("/", urlBits);
        var response = request.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", url);

        if (data != null)
        {
            await response.WriteAsJsonAsync(data).ConfigureAwait(false);
        }
        
        return response;
    }

    /// <summary>
    /// Create a ProblemDetails based response. This was based off the Microsoft implementation in the minimal api constructs.
    /// </summary>
    /// <param name="request">The <see cref="HttpResponseData"/> request.</param>
    /// <param name="errors">Dictionary or errors.</param>
    /// <param name="detail">Details value.</param>
    /// <param name="instance">Instance value.</param>
    /// <param name="statusCode">The associated status code.</param>
    /// <param name="title">The title of the error. If none supplied it will default to ""One or more validation errors occurred.".</param>
    /// <param name="type">The type value.</param>
    /// <returns>A <see cref="HttpResponseData"/> instance containing a ProblemDetails construct.</returns>
    public static async Task<HttpResponseData> ValidationResponse(this HttpRequestData request, 
        IDictionary<string, string[]> errors,
        string? detail = null,
        string? instance = null,
        int? statusCode = null,
        string? title = null,
        string? type = null)
    {
        var problemDetails = new HttpValidationProblemDetails(errors)
        {
            Detail = detail,
            Instance = instance,
            Type = type,
            Status = statusCode,
        };

        if (!string.IsNullOrWhiteSpace(title))
        {
            problemDetails.Title = title;
        }

        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(problemDetails).ConfigureAwait(false);
        return response;
    }
}