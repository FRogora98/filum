using Filum.SampleHost;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// The host's own database, with the engine's tables on its own context.
var connectionString = builder.Configuration.GetConnectionString("sample")
    ?? throw new InvalidOperationException("Set ConnectionStrings:sample to a PostgreSQL database.");
builder.Services.AddDbContextFactory<SampleDbContext>(options => options.UseNpgsql(connectionString), ServiceLifetime.Scoped);

// The turn, from configuration (models, providers, the package, the assistant's name), and the host's own tools.
builder.Services.AddFilumAgent<SampleDbContext>(builder.Configuration);
builder.Services.AddScoped<ITurnToolSource, ClockTools>();

// The contract of the groups as OpenAPI (spec 019), for the host's app to generate its client from.
builder.Services.AddOpenApi();
// Numbers are numbers: a generated client gets exact types (the web default also reads numbers written as strings).
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

var app = builder.Build();
HostTools.CheckNames(app.Services);

// A real host keeps migrations; the sample creates its tables once.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
}

app.MapOpenApi("/sample/openapi/{documentName}.json");

// A host maps the groups it wants: the memory group can be left out (Sample:MapMemory = false).
var sample = app.MapGroup("/sample");
sample.MapFilumModels();
var person = sample.MapGroup(string.Empty)
    .RequirePerson(SamplePerson)
    .MapFilumConversations()
    .MapFilumUsage()
    .MapFilumSkills();
if (app.Configuration.GetValue("Sample:MapMemory", true))
{
    person.MapFilumMemory();
}

app.Run();

// SAMPLE ONLY, NOT A LOGIN: the person is whatever id the caller puts in a header. A real host reads the person from
// its own authentication (a claim of a verified token), never from something the caller can simply choose.
static Guid? SamplePerson(HttpContext http) =>
    Guid.TryParse(http.Request.Headers["X-Sample-Person"], out var person) ? person : null;

public partial class Program;
