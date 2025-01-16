using Microsoft.EntityFrameworkCore;

namespace DataAccess;

public class DataContext(DbContextOptions<DataContext> contextOptions) : DbContext(contextOptions);
