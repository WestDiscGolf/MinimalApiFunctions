using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

string connectionString = "Server=(localdb)\\mssqllocaldb;Database=TodoContext-13D62DBB-69B7-4734-B4B2-90796EF06F3A;Trusted_Connection=True;MultipleActiveResultSets=true";

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddDbContext<TodoDb>(options =>
            //options.UseSqlServer(Configuration.GetConnectionString("SpotterContext")));
            options.UseSqlServer(connectionString)
        );
    })
    .Build();

await CreateDbIfNotExists();
await host.RunAsync();

async Task CreateDbIfNotExists()
{
    var options = new DbContextOptionsBuilder<TodoDb>().UseSqlServer(connectionString).Options;
    using var db = new TodoDb(options);
    await db.Database.EnsureCreatedAsync();
    // todo: migrations
}

public class Todo
{
    public int Id { get; set; }
    [Required]
    public string? Title { get; set; }
    public bool IsComplete { get; set; }
}

public class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}

public static class Functions
{
    [Function("hello-text")]
    public static async Task<HttpResponseData> Root([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "text")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Hello World!");
        return response;
    }

    [Function("hello-json")]
    public static async Task<HttpResponseData> HelloJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hello")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Hello = "World" });
        return response;
    }

    [Function("todo-list")]
    public static async Task<HttpResponseData> Todos([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req, FunctionContext executionContext)
    {
        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(await db.Todos.ToListAsync());
        return response;
    }

    [Function("todo-find")]
    public static async Task<HttpResponseData> TodosFindById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id:int}")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(todo);
            return response;
        }
        else
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            return response;
        }
    }

    [Function("todo-list-complete")]
    public static async Task<HttpResponseData> TodosComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/complete")] HttpRequestData req, FunctionContext executionContext)
    {
        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(await db.Todos.Where(x => x.IsComplete).ToListAsync());
        return response;
    }

    [Function("todo-list-incomplete")]
    public static async Task<HttpResponseData> TodosInComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/incomplete")] HttpRequestData req, FunctionContext executionContext)
    {
        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(await db.Todos.Where(x => !x.IsComplete).ToListAsync());
        return response;
    }

    [Function("todo-post")]
    public static async Task<HttpResponseData> TodosPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req, FunctionContext executionContext)
    {
        // todo: validation?

        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();

        var todo = await req.ReadFromJsonAsync<Todo>();
        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(todo);

        // todo: created(route/{id}, todo);

        return response;
    }

    [Function("todo-put")]
    public static async Task<HttpResponseData> TodosPut([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:int}")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        // todo: validation?
        // is the id in route the same as the body?

        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();

        var todo = await db.Todos.FindAsync(id);
        if (todo == null) { return req.CreateResponse(HttpStatusCode.NotFound); }

        var inputTodo = await req.ReadFromJsonAsync<Todo>();

        todo.Title = inputTodo.Title;
        todo.IsComplete = inputTodo.IsComplete;

        await db.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.NoContent);
        await response.WriteAsJsonAsync(todo);

        // todo: created(route/{id}, todo);

        return response;
    }

    [Function("todo-mark-complete")]
    public static async Task<HttpResponseData> TodosMarkComplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:int}/mark-complete")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        // todo: validation?
        // is the id in route the same as the body?

        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();

        var todo = await db.Todos.FindAsync(id);
        if (todo == null) { return req.CreateResponse(HttpStatusCode.NotFound); }

        todo.IsComplete = true;

        await db.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.NoContent);

        // todo: created(route/{id}, todo);

        return response;
    }

    [Function("todo-mark-incomplete")]
    public static async Task<HttpResponseData> TodosMarkIncomplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:int}/mark-incomplete")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        // todo: validation?
        // is the id in route the same as the body?

        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();

        var todo = await db.Todos.FindAsync(id);
        if (todo == null) { return req.CreateResponse(HttpStatusCode.NotFound); }

        todo.IsComplete = false;

        await db.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.NoContent);

        // todo: created(route/{id}, todo);

        return response;
    }

    [Function("todo-delete")]
    public static async Task<HttpResponseData> TodosDelete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id:int}")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        // todo: validation?
        // is the id in route the same as the body?

        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();

        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            db.Todos.Remove(todo);
            await db.SaveChangesAsync();
            return req.CreateResponse(HttpStatusCode.OK);
        }

        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    [Function("todo-delete-all")]
    public static async Task<HttpResponseData> TodosDeleteAll([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/delete-all")] HttpRequestData req, FunctionContext executionContext)
    {
        var db = executionContext.InstanceServices.GetRequiredService<TodoDb>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Todos");
        return req.CreateResponse(HttpStatusCode.OK);
    }
}