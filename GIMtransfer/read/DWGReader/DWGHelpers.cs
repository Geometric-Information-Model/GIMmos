using ACadSharp.Entities;
using CSMath;
using GIMcompress;
using GIMcompress.Shrinkwrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMtransfer
{
    public static class DWGHelpers
    {
        public static Vect Vertex2DtoVect(LwPolyline.Vertex vertex)
        {
            return new(vertex.Location.X, vertex.Location.Y, 0);
        }

        public static Vect Vertex2DtoVect(Vertex vertex)
        {
            return new(vertex.Location.X, vertex.Location.Y, 0);
        }

        public static Vect VertexToVect(VertexFaceMesh vertex)
        {
            return new(vertex.Location.X, vertex.Location.Y, vertex.Location.Z);
        }

        public static Vect XYZtoVect(XYZ xyz)
        {
            return new(xyz.X, xyz.Y, xyz.Z);
        }

        public static Vect XYZWithBulgeToVect(Vertex vertex)
        {
            // Using Z to store bulge value
            return new(vertex.Location.X, vertex.Location.Y, vertex.Bulge);
        }

        public static Vect XYZWithBulgeToVect(LwPolyline.Vertex vertex)
        {
            // Using Z to store bulge value
            return new(vertex.Location.X, vertex.Location.Y, vertex.Bulge);
        }

        public static List<Vect> Face3DPointsToVectList(Face3D face3D)
        {
            HashSet<Vect> points = new();
            Vect firstCorner = XYZtoVect(face3D.FirstCorner);
            points.Add(firstCorner);
            Vect secondCorner = XYZtoVect(face3D.SecondCorner);
            points.Add(secondCorner);
            Vect thirdCorner = XYZtoVect(face3D.ThirdCorner);
            points.Add(thirdCorner);
            Vect fourthCorner = XYZtoVect(face3D.FourthCorner);
            points.Add(fourthCorner);

            return points.ToList();
        }

        public static List<Vect> SolidPointsToVectList(Solid solid)
        {
            HashSet<Vect> points = new();
            Vect firstCorner = XYZtoVect(solid.FirstCorner);
            points.Add(firstCorner);
            Vect secondCorner = XYZtoVect(solid.SecondCorner);
            points.Add(secondCorner);
            Vect thirdCorner = XYZtoVect(solid.ThirdCorner);
            points.Add(thirdCorner);
            Vect fourthCorner = XYZtoVect(solid.FourthCorner);
            points.Add(fourthCorner);

            return points.ToList();
        }

        public static List<Triangle> GetTwoTrianglesFromFourPoints(List<Vect> points)
        {
            List<Triangle> triangles = new();
            var norm1 = (points[0] - points[3]).Norm;
            var norm2 = (points[1] - points[2]).Norm;
            if(norm1 < norm2)
            {
                
                Triangle triangle1 = new(points[0], points[1], points[3]);
                Triangle triangle2 = new(points[0], points[2], points[3]);
                triangles.Add(triangle1);
                triangles.Add(triangle2);
            }
            else
            {
                Triangle triangle1 = new(points[1], points[0], points[2]);
                Triangle triangle2 = new(points[1], points[3], points[2]);
                triangles.Add(triangle1);
                triangles.Add(triangle2);
            }

            return triangles;

        }
    }
}
