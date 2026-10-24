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
    internal class DemoDBContext : DbContext
    {
        internal const string localConnectionString = "Server=127.0.0.1; Port=3306; Database=cityjsontests; Uid=cityjson; Pwd=cityjson";

        internal static string GetBackendConnectionString()
        {
            string connectionString = "";
            string server = "phpstack-6421988247234627.kloudbeansite.com";
            string db = "kb_8453lchrzd";
            string uid = "kb_8453lchrzd";
            string pwd = "lEouj10AXCQJtef";

            connectionString = 
                "SERVER=" + server + ";" +
                "DATABASE=" + db + ";" +
                "uid=" + uid + ";" +
                "password=" + pwd;
            
            return connectionString;
        }
        public DbSet<DemoDBPlot> Plots { get; set; }
        public DbSet<DemoDBObject> Objects { get; set; }
        public DbSet<DemoDBObjectPlotRelation> PlotObjectRelations { get; set; }

        private string connectionString = "";
        public DemoDBContext(string connectionString = localConnectionString)
        {
            this.connectionString = connectionString;
            Database.SetConnectionString(connectionString);
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(ServerVersion.AutoDetect(this.connectionString));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DemoDBObjectPlotRelation>().HasKey(m => new { m.PlotId, m.ObjectId });
            modelBuilder.Entity<DemoDBPlot>().Property(plot => plot.Type).HasConversion(
                value => value,
                value => value
                );

            modelBuilder.Entity<DemoDBObject>().Property(obj => obj.Type).HasConversion(
                value => value,
                value => value
                );
        }

    }

        [Table(name:"cityjsonplot")]
    internal class DemoDBPlot
    {
        [Column("Id")]
        [Key]
        [Required(ErrorMessage = "ID Cannot be null.")]
        public int Id { get; set; }


        [Column("Name")]
        [Required(ErrorMessage = "Name Cannot be null.")]
        public string? Name { get; set; }


        [Column("Type")]
        [Required(ErrorMessage = "Object Type Cannot be null.")]
        public string Type { get; set; }


        [Column("Px")]
        [Required(ErrorMessage = "Position X Cannot be null.")]
        public double Px { get; set; }


        [Column("Py")]
        [Required(ErrorMessage = "Position Y Cannot be null.")]
        public double Py { get; set; }


        [Column("MinGimLat")]
        [Required(ErrorMessage = "MinGimLat Cannot be null.")]
        public int MinGimLat { get; set; } // Latitude / 360 * 40075


        [Column("MaxGimLat")]
        [Required(ErrorMessage = "MaxGimLat Cannot be null.")]
        public int MaxGimLat { get; set; } // Latitude / 360 * 40075


        [Column("MinGimLong")]
        [Required(ErrorMessage = "MinGimLong Cannot be null.")]
        public int MinGimLong { get; set; } // Longitude / 360 * 40075


        [Column("MaxGimLong")]
        [Required(ErrorMessage = "MaxGimLong Cannot be null.")]
        public int MaxGimLong { get; set; } // Longitude / 360 * 40075

        [Column("BeginDate", TypeName = ("Date"))]
        public DateTime BeginDate { get; set; }
        [Column("EndDate", TypeName = ("Date"))]
        public DateTime EndDate { get; set; }

        [Column("GimBin")]
        [Required(ErrorMessage = "GimBin Cannot be null.")]
        public string? GimBin { get; set; }


        [Column("GimJson")]
        [Required(ErrorMessage = "GimJson Cannot be null.")]
        public string? GimJson { get; set; } // maybe not needed
    }

    internal enum PlotType
    {
        road, waterbody, landuse, plantcover
    }


    [Table(name:"cityjsonobject")]
    internal class DemoDBObject
    {
        [Column("Id")]
        [Key]
        [Required(ErrorMessage="ID Cannot be null.")]
        public int Id { get; set; }


        [Column("Name")]
        [Required(ErrorMessage = "Name Cannot be null.")]
        public string? Name { get; set; }


        [Column("Type")]
        [Required(ErrorMessage = "Object Type Cannot be null.")]
        public string Type { get; set; }


        [Column("Px")]
        [Required(ErrorMessage = "Position X Cannot be null.")]
        public double Px { get; set; }


        [Column("Py")]
        [Required(ErrorMessage = "Position Y Cannot be null.")]
        public double Py { get; set; }


        [Column("Pz")]
        [Required(ErrorMessage = "Position Z Cannot be null.")]
        public double Pz { get; set; }


        [Column("Rp")]
        [Required(ErrorMessage = "Rotation P Cannot be null.")]
        public double Rp { get; set; }


        [Column("Ry")]
        [Required(ErrorMessage = "Rotation Y Cannot be null.")]
        public double Ry { get; set; }


        [Column("Rr")]
        [Required(ErrorMessage = "Rotation R Cannot be null.")]
        public double Rr { get; set; }

        [Column("OID")]
        public int OID { get; set; }

        [Column("PID")]
        public int PID { get; set; }


        [Column("GimBin")]
        [Required(ErrorMessage = "GimBin Cannot be null.")]
        public string? GimBin { get; set; }


        [Column("GimJson")]
        [Required(ErrorMessage = "GimJson Cannot be null.")]
        public string? GimJson { get; set; } // maybe not needed

        [Column("BeginDate", TypeName = ("Date"))]
        public DateTime BeginDate { get; set; }
        [Column("EndDate", TypeName = ("Date"))]
        public DateTime EndDate { get; set; }
    }

    internal enum ObjectType
    {
        building, genericcityobject, bridge
    }


    [Table(name: "cityjsonplotobjectrelation")]
    internal class DemoDBObjectPlotRelation
    {
        [Column("PlotId")]
        [ForeignKey("cityjsonplot")]
        public int PlotId { get; set; }

        [Column("ObjectId")]
        [ForeignKey("cityjsonobject")]
        public int ObjectId { get; set; }
    }

}
