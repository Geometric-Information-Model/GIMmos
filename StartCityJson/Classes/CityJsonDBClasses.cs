using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using CityJson;
using GIMcompress;
using Microsoft.EntityFrameworkCore.Metadata;

namespace StartCityJson
{
    internal class CityJsonDBContext : DbContext, IDisposable
    {
        internal const string localConnectionString = "Server=127.0.0.1; Port=3306; Database=cityjsontests; Uid=cityjson; Pwd=cityjson";

        public DbSet<ObjectDefinition> ObjectDefinitions { get; set; }
        public DbSet<ObjectInstance> ObjectInstances { get; set; }
        public DbSet<Altitudes> Altitudes { get; set; }
        public DbSet<RegionBoundaries> RegionBoundaries { get; set; }
        //public DbSet<ObjectVisualisation> ObjectVisualisations { get; set; }
        public string connectionString = "";

        public CityJsonDBContext(string connectionString = localConnectionString)
        {
            this.connectionString = connectionString;
            Database.SetConnectionString(connectionString);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Altitudes>().HasKey(m => new { m.X, m.Y });
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(ServerVersion.AutoDetect(this.connectionString));
            
        }

    }

    [Table(name: "ObjectDefinition")]
    internal class ObjectDefinition
    {
        [Column("Name")]
        public string Name { get; set; }
        [Column("Type")]
        public string Type { get; set; }

        [Column("ID")]
        [Key]
        [Required(ErrorMessage = "ID Cannot be null.")]
        public int ID { get; set; }

        [Column("PID")]
        public int? PID { get; set; }

        [Column("OID")]
        public int? OID { get; set; }
        [Column("minGIMunit")]
        public byte minGIMunit { get; set; }

        [Column("maxGIMunit")]
        public byte maxGIMunit { get; set; }

        public override string ToString()
        {
            return $"{ID};{PID};{OID};{Name};{Type};{minGIMunit};{maxGIMunit}";
        }

    }
    [Table(name: "ObjectInstance")]
    internal class ObjectInstance
    {
        [Column("Name")]
        [Required(ErrorMessage = "Name Cannot be null.")]
        public string Name { get; set; }

        [Column("RegionID")]
        public string RegionID { get; set; }

        [Column("ID")]
        [Key]
        [Required(ErrorMessage = "ID Cannot be null.")]
        public int ID { get; set; }


        [Column("DefID")]
        [ForeignKey("ObjectDefinition")]
        public int? DefID { get; set; }

        [Column("Px")]
        [Required(ErrorMessage = "Position X Cannot be null.")]
        public int Px { get; set; }


        [Column("Py")]
        [Required(ErrorMessage = "Position Y Cannot be null.")]
        public int Py { get; set; }

        [Column("Pz")]
        [Required(ErrorMessage = "Position Z Cannot be null.")]
        public int Pz { get; set; }

        [Column("Ry")]
        [Required(ErrorMessage = "Rotation Y Cannot be null.")]
        public int Ry { get; set; }

        [Column("GIMminlat")]
        [Required(ErrorMessage = "GIMminlat Cannot be null.")]
        public int GIMminlat { get; set; }

        [Column("GIMmaxlat")]
        [Required(ErrorMessage = "GIMmaxlat Cannot be null.")]
        public int GIMmaxlat { get; set; }

        [Column("GIMminlong")]
        [Required(ErrorMessage = "GIMminlong Cannot be null.")]
        public int GIMminlong { get; set; }

        [Column("GIMmaxlong")]
        [Required(ErrorMessage = "GIMmaxlong Cannot be null.")]
        public int GIMmaxlong { get; set; }
        [Column("M")]
        public bool M { get; set; } = false;

        [Column("StartDate")]
        public DateTime StartDate { get; set; } = DateTime.Parse("0001-01-01");
        [Column("EndDate")]
        public DateTime EndDate { get; set; } = DateTime.Parse("9999-12-31");

        public override string ToString()
        {
            return $"{ID};{DefID};{Px};{Py};{Pz};{Ry};{GIMminlat};{GIMmaxlat};{GIMminlong};{GIMmaxlong};{Name};{RegionID};{M};0000-01-01;9999-12-31";
        }
    }

    [Table(name: "Altitudes")]
    internal class Altitudes
    {
        [Key]
        [Column("X")]
        public int X { get; set; }
        [Key]
        [Column("Y")]
        public int Y { get; set; }
        [Column("Z")]
        public int Z { get; set; }
    }

    [Table(name: "RegionBoundaries")]
    internal class RegionBoundaries
    {
        [Key]
        [Column("ID")]
        public string ID { get; set; }

        [Column("minX")]
        public int minX { get; set; }

        [Column("minY")]
        public int minY { get; set; }

        [Column("maxX")]
        public int maxX { get; set; }

        [Column("maxY")]
        public int maxY { get; set; }
        [Column("GIMminlong")]
        public int GIMminlong { get; set; }
        [Column("GIMminlat")]
        public int GIMminlat { get; set; }

        [Column("GIMmaxlong")]
        public int GIMmaxlong { get; set; }
        [Column("GIMmaxlat")]
        public int GIMmaxlat { get; set; }

        internal int centerX;
        internal int centerY;

        public override string ToString()
        {
            return "Center[ " + centerX + ", " + centerY + ", ID = " + ID;
        }
    }

    //[Table(name: "ObjectVisualization")]
    //internal class ObjectVisualisation
    //{
    //    [Key, Column(Order = 0)]
    //    [ForeignKey("ObjectDefinition")]
    //    public int DefID { get; set; }

    //    [Key, Column(Order = 1)]
    //    [Required(ErrorMessage = "GIMUnit Cannot be null.")]
    //    public int GIMUnit { get; set; } = 0;

    //    [Column("GLB")]
    //    [Required(ErrorMessage = "GLB Cannot be null.")]
    //    public string? GLB { get; set; }

    //    [Column("PlotPoly")]
    //    public string? PlotPoly { get; set; } = "";
    //    public string? FileRef { get; set; } = "";
    //}

}
