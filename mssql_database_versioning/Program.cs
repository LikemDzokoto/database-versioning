using Microsoft.EntityFrameworkCore;
using mssql_database_version.Data;


var builder = WebApplication.CreateBuilder(args);


//database connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


//controller support 
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Globally prevents the "Object Cycle" crash
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;

        // Makes the JSON look pretty in Postman/Browser
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var app = builder.Build();

app.MapControllers();
app.Run();

