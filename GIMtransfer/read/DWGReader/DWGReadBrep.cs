using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACadSharp.Objects;
using ACadSharp.IO;
using ACadSharp.Entities;
using GIMcompress;
using CSMath;
using ACadSharp.Tables;
using GimUtilities;

namespace GIMtransfer
{
    public class DWGReadBrep
    {
        public static void ReadPolyrepPart(GimData gimData, uint partIndex, BlockRecord record)
        {
            GimElement polyRepElement = new GimElement()
            {
                ElementType = GimElementType.R_Poly
            };

            uint polyRepIndex = (uint)gimData.Elements.Count;
            gimData.Elements.Add(polyRepElement);
            gimData.AddChildToParent(polyRepIndex, partIndex);

            foreach (var entity in record.Entities)
            {
                ReadBrepEntity(gimData, polyRepIndex, entity);
            }
        }

        public static void ReadBrepEntity(GimData gimData, uint polyRepIndex, Entity entity)
        {
            switch (entity.ObjectType)
            {
                case ACadSharp.ObjectType.INSERT: return;
                // polygon mesh
                case ACadSharp.ObjectType.LWPOLYLINE:break;
            }
        }

        public static void ReadLWPolyline(GimData gimData, LwPolyline lwPolyline)
        {
            bool closed = lwPolyline.IsClosed;
            if (!closed)
            {
                GimLogger.Warn("Polyline not closed is not yet supported for breps!");
                return;
            }

            GimElement planeElement = new()
            {
                ElementType = GimElementType.F_plane
            };

            List<Vect> points = lwPolyline.Vertices.Select(
                (vertex) => new Vect(vertex.Location.X, vertex.Location.Y, 0)).ToList();


        }

        public GimElement GetLineCurve(Entity entity)
        {
            Line lineEntity = (Line)entity;
            GimElement lineCurve = new GimElement()
            {
                ElementType = GimElementType.C_line
            };

            var startPointDWG = lineEntity.StartPoint;
            var endPointDWG = lineEntity.EndPoint;
            Vect startPointGIM = new(startPointDWG.X, startPointDWG.Y, startPointDWG.Z);
            Vect endPointGIM = new(endPointDWG.X, endPointDWG.Y, endPointDWG.Z);
            var norm = (endPointGIM - startPointGIM).Norm;
            var direction = (endPointGIM - startPointGIM).Direction;
            double[] lineDoubles = new double[]
            {
                0,
                norm
            };
            direction.ApplyToLocal(lineCurve.Locals[0]);
            lineCurve.Locals[0].px = startPointGIM[0];
            lineCurve.Locals[0].py = startPointGIM[1];
            lineCurve.Locals[0].pz = startPointGIM[2];
            lineCurve.elementDoubles = lineDoubles;
            return lineCurve;
        }


        public GimElement GetEllipseCurve(Entity entity)
        {
            Ellipse ellipseEntity = (Ellipse)entity;
            GimElement ellipseCurve = new GimElement()
            {
                ElementType = GimElementType.C_elipse
            };
            var centerDWG = ellipseEntity.Center;
            Vect centerGIM = new(centerDWG.X, centerDWG.Y, centerDWG.Z);
            double[] ellipseDoubles = new double[]
            {
                ellipseEntity.MajorAxis,
                ellipseEntity.MinorAxis,
                ellipseEntity.StartParameter,
                ellipseEntity.EndParameter
            };
            ellipseCurve.Locals[0].px = centerGIM[0];
            ellipseCurve.Locals[0].py = centerGIM[1];
            ellipseCurve.Locals[0].pz = centerGIM[2];
            return ellipseCurve;
        }


        public GimElement GetArcCurve(Entity entity)
        {
            Arc arcEntity = (Arc)entity;
            GimElement circleCurve = new GimElement()
            {
                ElementType = GimElementType.C_circle
            };
            var centerDWG = arcEntity.Center;
            Vect centerGIM = new(centerDWG.X, centerDWG.Y, centerDWG.Z);

            // CHECK HOW TO PROCEED IF START ANGLE > END ANGLE
            // AND DO THE NECESSARY CHANGES IF NEEDED

            double[] circleDoubles = new double[]
            {
                arcEntity.Radius,
                arcEntity.StartAngle,
                arcEntity.EndAngle
            };

            return circleCurve;
        }

        public GimElement GetCircleCurve(Entity entity)
        {
            Circle circleEntity = (Circle)entity;
            GimElement circleCurve = new GimElement()
            {
                ElementType = GimElementType.C_circle
            };
            var centerDWG = circleEntity.Center;
            Vect centerGIM = new(centerDWG.X, centerDWG.Y, centerDWG.Z);
            double[] circleDoubles = new double[]
            {
                circleEntity.Radius,
                0,
                Math.PI * 2
            };

            circleCurve.Locals[0].px = centerGIM[0];
            circleCurve.Locals[0].py = centerGIM[1];
            circleCurve.Locals[0].pz = centerGIM[2];


            return circleCurve;
        }

       


        public void SetPlaneSurfaceFrom3DFace(Entity entity, int parentIndex, GimData gimData)
        {

            GimElement planeElement = new() { elementType = GimElementType.F_plane };
            int planeIndex = gimData.elements.Count;
            gimData.elements.Add(planeElement);
            gimData.AddChildToParent((uint)planeIndex, (uint)parentIndex);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)planeIndex);
            Vect sumOfPoints = new(0, 0, 0);

            Face3D face3DEntity = (Face3D)entity;
            var firstCornerDWG = face3DEntity.FirstCorner;
            Vect firstCornerGIM = new(firstCornerDWG.X, firstCornerDWG.Y, firstCornerDWG.Z);
            var secondCornerDWG = face3DEntity.SecondCorner;
            Vect secondCornerGIM = new(secondCornerDWG.X, secondCornerDWG.Y, secondCornerDWG.Z);
            var thirdCornerDWG = face3DEntity.ThirdCorner;
            Vect thirdCornerGIM = new(thirdCornerDWG.X, thirdCornerDWG.Y, thirdCornerDWG.Z);
            var fourthCornerDWG = face3DEntity.FourthCorner;
            Vect fourthCornerGIM = new(fourthCornerDWG.X, fourthCornerDWG.Y, fourthCornerDWG.Z);
            List<Vect> points = new() {
                firstCornerGIM,
                secondCornerGIM,
                thirdCornerGIM,
                fourthCornerGIM

            };

            if (points[3] == points[2])
                points.RemoveAt(3);

            CreatePlane(points, gimData, planeIndex);


        }


        public void SetPlaneSurfaceFromLine(Entity entity, int parentIndex, GimData gimData)
        {
            GimElement planeElement = new() { elementType = GimElementType.F_plane };
            int planeIndex = gimData.elements.Count;
            gimData.elements.Add(planeElement);
            gimData.AddChildToParent((uint)planeIndex, (uint)parentIndex);
            GimElement wireElement = new() { elementType = GimElementType.wire };
            int wireIndex = gimData.elements.Count;
            gimData.elements.Add(wireElement);
            gimData.AddChildToParent((uint)wireIndex, (uint)planeIndex);
            Vect sumOfPoints = new(0, 0, 0);
            Line lineEntity = (Line)entity;
            List<Vect> points = GetPointsFromExtrudedLine(lineEntity);
            CreatePlane(points, gimData, planeIndex);

        }


        public List<Vect> GetPointsFromExtrudedLine(Line lineEntity) 
        {
            List<Vect> points = new();
            var startPointDWG = lineEntity.StartPoint;
            var endPointDWG = lineEntity.EndPoint;
            Vect firstPoint = new(startPointDWG.X, startPointDWG.Y, startPointDWG.Z);
            points.Add(firstPoint);
            Vect secondPoint = new(startPointDWG.X, startPointDWG.Y, startPointDWG.Z);
            points.Add(secondPoint);
            double extrusionLength = lineEntity.Thickness;
            Vect extrusionDirection = new(lineEntity.Normal.X, lineEntity.Normal.Y, lineEntity.Normal.Z);
            Vect finalVect = extrusionLength * extrusionDirection;
            Vect thirdPoint = secondPoint + finalVect;
            points.Add(thirdPoint);
            Vect fourthPoint = firstPoint + finalVect;
            points.Add(fourthPoint);
            return points;
        }


        public void CreatePlane(List<Vect> points, GimData gimData, int planeIndex)
        {
            GimElement planeElement = gimData.Elements[planeIndex];
            Vect sumOfPoints = new(0, 0, 0);
            for (int i = 0; i < points.Count; i++)
            {
                GimElement line = new() { elementType = GimElementType.C_line };
                Vect currentPoint = points[i];
                sumOfPoints = +currentPoint;
                int nextIndex = (i + 1) % points.Count;
                Vect nextPoint = points[nextIndex];
                var norm = (nextPoint - currentPoint).Norm;
                var direction = (nextPoint - currentPoint).Direction;
                double[] lineDoubles = new double[]
                {
                    0,
                    norm
                };
                direction.ApplyToLocal(line.Locals[0]);
                line.Locals[0].px = currentPoint[0];
                line.Locals[0].py = currentPoint[1];
                line.Locals[0].pz = currentPoint[2];
                line.elementDoubles = lineDoubles;
                gimData.elements.Add(line);
            }

            Vect centerPoint = sumOfPoints / points.Count;
            Vect directionA = new(0, 0, 0);
            double maxLen = 0;

            for (int i = 0; i < points.Count; i++)
            {
                int firstIndex = (i - 1 + points.Count) % points.Count;
                int lastIndex = i;
                Vect lastPointGIM = points[lastIndex];
                Vect firstPointGIM = points[firstIndex];
                Vect segment = lastPointGIM - firstPointGIM;
                // logging highest Z value


                if (maxLen < segment.Norm)
                {
                    directionA = segment;
                    maxLen = segment.Norm;
                }
            }

            directionA = directionA.Direction;

            Vect directionB = new Vect(0, 0, 0);

            // maximum perpendicularity coefficient: we want directionB to be as less parallel with A, to guarantee the most precise normal 
            double maxCoeff = 0;
            bool degenerate = true;
            for (int i = 0; i < points.Count; i++)
            {
                int firstIndex = (i - 1 + points.Count) % points.Count;
                int lastIndex = i;
                Vect lastPointGIM = points[lastIndex];
                Vect firstPointGIM = points[firstIndex];
                Vect segment = lastPointGIM - firstPointGIM;
                double coeff = (segment ^ directionA).Norm;
                if (coeff > maxCoeff)
                {
                    maxCoeff = coeff;
                    directionB = segment;
                }
            }

            if (!directionB.IsCollinearTo(directionA))
            {
                degenerate = false;
            }

            if (!degenerate)
            {
                directionB = (directionB - directionA * directionB * directionA).Direction;

                var directionC = directionB ^ directionA;
                Matrix matrix = new(directionA, directionB, directionC);
                matrix.ApplyToLocal(planeElement.Locals[0]);
                planeElement.locals[0].px = centerPoint[0];
                planeElement.locals[0].py = centerPoint[1];
                planeElement.locals[0].pz = centerPoint[2];
            }
            else
            {
                List<uint> planeIndexes = new();
                planeIndexes.Add((uint)planeIndex);
                gimData.RemoveElementsAndTheirChildren(planeIndexes);
            }
        }



    }
}
