using Microsoft.EntityFrameworkCore;

namespace DataAccess;

public class DataContext (DbContextOptions<DataContext> contextOptions): DbContext(contextOptions)
{
    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     modelBuilder.Entity<User>()
    //         .Property(u => u.Id)
    //         .ValueGeneratedOnAdd(); // Указывает, что Id будет сгенерирован при добавлении
    // }

}