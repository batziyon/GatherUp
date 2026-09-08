using System.Reflection;
using System.Text;
using GatherUp.API.Middleware;
using GatherUp.BL;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;
using GatherUp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

string xmlFolder      = Path.Combine(builder.Environment.ContentRootPath, "XMLData");
string receiptsFolder = Path.Combine(builder.Environment.ContentRootPath, "ReceiptsStorage");
string emailsFolder   = Path.Combine(builder.Environment.ContentRootPath, "EmailsLog");

var jwtSection = builder.Configuration.GetSection("Jwt");
string jwtKey      = jwtSection["Key"]!;
string jwtIssuer   = jwtSection["Issuer"]!;
string jwtAudience = jwtSection["Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IRepository<Event>>(_ =>
    new XmlRepository<Event>(xmlFolder));

builder.Services.AddSingleton<IRepository<Participant>>(_ =>
    new XmlRepository<Participant>(xmlFolder));

builder.Services.AddSingleton<IRepository<VendorAllocation>>(_ =>
    new XmlRepository<VendorAllocation>(xmlFolder));

builder.Services.AddSingleton<IRepository<Poll>>(_ =>
    new XmlRepository<Poll>(xmlFolder));

builder.Services.AddSingleton<IRepository<EventManager>>(_ =>
    new XmlRepository<EventManager>(xmlFolder));

builder.Services.AddSingleton<IRepository<EventHost>>(_ =>
    new XmlRepository<EventHost>(xmlFolder));

builder.Services.AddSingleton<IRepository<Receipt>>(_ =>
    new ReceiptRepository(xmlFolder, receiptsFolder));

builder.Services.AddSingleton<IEmailService>(_ =>
    new FileEmailService(emailsFolder));

builder.Services.AddSingleton<IEventNotifier, EventNotifierService>();
builder.Services.AddSingleton<EventService>(sp => new EventService(
    sp.GetRequiredService<IRepository<Event>>(),
    sp.GetRequiredService<IRepository<EventManager>>(),
    sp.GetRequiredService<IRepository<EventHost>>(),
    sp.GetRequiredService<IRepository<Participant>>(),
    sp.GetRequiredService<IEmailService>(),
    sp.GetRequiredService<IEventNotifier>()));
builder.Services.AddSingleton<PersonService>();
builder.Services.AddSingleton<ParticipantService>(sp => new ParticipantService(
    sp.GetRequiredService<IRepository<Participant>>(),
    sp.GetRequiredService<IRepository<Event>>(),
    sp.GetRequiredService<IEmailService>(),
    sp.GetRequiredService<IEventNotifier>()));
builder.Services.AddSingleton<FinanceService>(sp => new FinanceService(
    sp.GetRequiredService<IRepository<Participant>>(),
    sp.GetRequiredService<IRepository<VendorAllocation>>(),
    sp.GetRequiredService<IRepository<Receipt>>(),
    sp.GetRequiredService<IRepository<Event>>(),
    sp.GetRequiredService<IEmailService>(),
    sp.GetRequiredService<IEventNotifier>()));
builder.Services.AddSingleton<PollService>(sp => new PollService(
    sp.GetRequiredService<IRepository<Poll>>(),
    sp.GetRequiredService<IRepository<Event>>(),
    sp.GetRequiredService<IRepository<Participant>>(),
    sp.GetRequiredService<IEmailService>(),
    sp.GetRequiredService<IEventNotifier>()));

builder.Services.AddSingleton<EmailLogService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "GatherUp API",
        Version     = "v1",
        Description = "REST API למערכת ניהול האירועים GatherUp. Login → העתק Token → לחץ Authorize 🔒"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "הכנס JWT Token בפורמט: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    options.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
    options.DocInclusionPredicate((_, _) => true);
});

var app = builder.Build();

if (!File.Exists(Path.Combine(xmlFolder, "Events.xml")))
{
    await InitializeData.InitializeAsync(
        app.Services.GetRequiredService<IRepository<Event>>(),
        app.Services.GetRequiredService<IRepository<Participant>>(),
        app.Services.GetRequiredService<IRepository<VendorAllocation>>(),
        app.Services.GetRequiredService<IRepository<Poll>>(),
        app.Services.GetRequiredService<IRepository<EventManager>>(),
        app.Services.GetRequiredService<IRepository<EventHost>>());
}

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GatherUp API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

if (!Directory.Exists(receiptsFolder))
    Directory.CreateDirectory(receiptsFolder);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(receiptsFolder),
    RequestPath  = "/receipts"
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
