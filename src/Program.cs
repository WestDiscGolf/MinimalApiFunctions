var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddDbContext<TodoDb>(options =>
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

public class Functions
{
    private readonly TodoDb _db;

    public Functions(TodoDb db)
    {
        _db = db;
    }

    [Function("hello-text")]
    public async Task<HttpResponseData> Root([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "text")] HttpRequestData req)
    {
        return await req.OkResponse("Hello World!");
    }

    [Function("hello-json")]
    public async Task<HttpResponseData> HelloJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hello")] HttpRequestData req)
    {
        return await req.OkObjectResponse(new { Hello = "World" });
    }

    [Function("todo-list")]
    public async Task<HttpResponseData> Todos([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req, FunctionContext executionContext)
    {
        return await req.OkObjectResponse(await _db.Todos.ToListAsync());
    }

    [Function("todo-find")]
    public async Task<HttpResponseData> TodosFindById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id:int}")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        if (await _db.Todos.FindAsync(id) is Todo todo)
        {
            return await req.OkObjectResponse(todo);
        }
        return req.NotFoundResponse();
    }

    [Function("todo-list-complete")]
    public async Task<HttpResponseData> TodosComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/complete")] HttpRequestData req, FunctionContext executionContext)
    {
        return await req.OkObjectResponse(await _db.Todos.Where(x => x.IsComplete).ToListAsync());
    }

    [Function("todo-list-incomplete")]
    public async Task<HttpResponseData> TodosInComplete([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/incomplete")] HttpRequestData req, FunctionContext executionContext)
    {
        return await req.OkObjectResponse(await _db.Todos.Where(x => !x.IsComplete).ToListAsync());
    }

    [Function("todo-post")]
    public async Task<HttpResponseData> TodosPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req, FunctionContext executionContext)
    {
        var todo = await req.ReadFromJsonAsync<Todo>();

        if (!MinimalValidation.TryValidate(todo, out var errors))
        {
            return await req.ValidationResponse(errors);
        }

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();
        
        return await req.CreatedAtResponse(nameof(TodosFindById), new { id = todo.Id }, todo);
    }

    [Function("todo-put")]
    public async Task<HttpResponseData> TodosPut([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:int}")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        var inputTodo = await req.ReadFromJsonAsync<Todo>();

        if (!MinimalValidation.TryValidate(inputTodo, out var errors))
        {
            return await req.ValidationResponse(errors);
        }

        var todo = await _db.Todos.FindAsync(id);
        if (todo == null)
        {
            return req.NotFoundResponse();
        }

        todo.Title = inputTodo.Title;
        todo.IsComplete = inputTodo.IsComplete;

        await _db.SaveChangesAsync();

        return req.NoContentResponse();
    }

    [Function("todo-mark-complete")]
    public async Task<HttpResponseData> TodosMarkComplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:int}/mark-complete")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        if (await _db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = true;
            await _db.SaveChangesAsync();
            return req.NoContentResponse();
        }

        return req.NotFoundResponse();
    }

    [Function("todo-mark-incomplete")]
    public async Task<HttpResponseData> TodosMarkIncomplete([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id:int}/mark-incomplete")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        if (await _db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = false;
            await _db.SaveChangesAsync();
            return req.NoContentResponse();
        }

        return req.NotFoundResponse();
    }

    [Function("todo-delete")]
    public async Task<HttpResponseData> TodosDelete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id:int}")] HttpRequestData req, int id, FunctionContext executionContext)
    {
        if (await _db.Todos.FindAsync(id) is Todo todo)
        {
            _db.Todos.Remove(todo);
            await _db.SaveChangesAsync();
            return req.NoContentResponse();
        }

        return req.NotFoundResponse();
    }

    [Function("todo-delete-all")]
    public async Task<HttpResponseData> TodosDeleteAll([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/delete-all")] HttpRequestData req, FunctionContext executionContext)
    {
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Todos");
        return req.NoContentResponse();
    }
}

public class Todo
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; }

    public bool IsComplete { get; set; }
}

public class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}