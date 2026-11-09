using netDxf;
using netDxf.Entities;
using SymbolLibrary.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SymbolLibrary.Core.Services
{
    public class DxfSymbolReader
    {
        // -------------------------------------------------------
        // CHECK DXF FILE
        // -------------------------------------------------------

        public bool IsValidDxfFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) ||
                !File.Exists(filePath))
                return false;

            try
            {
                DxfDocument document = DxfDocument.Load(filePath);
                return document != null;
            }
            catch
            {
                return false;
            }
        }

        // -------------------------------------------------------
        // READ UNITS
        // -------------------------------------------------------

        public string ReadUnits(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    "DXF file not found.", filePath);

            DxfDocument document = DxfDocument.Load(filePath);

            return document.DrawingVariables.InsUnits.ToString();
        }

        private double ConvertToMillimetres(
            double value,
            string units)
        {
            return units switch
            {
                "Millimeters" => value,
                "Centimeters" => value * 10,
                "Meters" => value * 1000,
                "Inches" => value * 25.4,
                "Feet" => value * 304.8,
                "Unitless" => value,
                _ => value
            };
        }

        // -------------------------------------------------------
        // BASIC ENTITY INFORMATION
        // -------------------------------------------------------

        public int GetEntityCount(string filePath)
        {
            if (!IsValidDxfFile(filePath))
                return 0;

            try
            {
                DxfDocument document = DxfDocument.Load(filePath);
                return document.Entities.All.Count();
            }
            catch
            {
                return 0;
            }
        }

        public string GetEntityTypes(string filePath)
        {
            DxfDocument document = DxfDocument.Load(filePath);

            return
                $"Lines: {document.Entities.Lines.Count()}\n" +
                $"Circles: {document.Entities.Circles.Count()}\n" +
                $"Arcs: {document.Entities.Arcs.Count()}\n" +
                $"Polylines: {document.Entities.Polylines2D.Count()}\n" +
                $"Texts: {document.Entities.Texts.Count()}\n" +
                $"MTexts: {document.Entities.MTexts.Count()}\n" +
                $"Inserts: {document.Entities.Inserts.Count()}";
        }

        // -------------------------------------------------------
        // READ SYMBOL SIZE
        // -------------------------------------------------------

        public (double Width, double Height)
            ReadSymbolSizeInMillimetres(string filePath)
        {
            DxfBoundingBox box = ReadBasicSymbolBounds(filePath);

            if (!box.IsValid)
                return (0, 0);

            string units = ReadUnits(filePath);

            return (
                ConvertToMillimetres(box.Width, units),
                ConvertToMillimetres(box.Height, units)
            );
        }

        // -------------------------------------------------------
        // CREATE SYMBOL PLACEMENT
        // -------------------------------------------------------

        public SymbolPlacement CreateSymbolPlacement(
            string symbolId,
            string name,
            string filePath)
        {
            var size = ReadSymbolSizeInMillimetres(filePath);

            return new SymbolPlacement(
                symbolId,
                name,
                Path.GetFullPath(filePath),
                null,
                "Millimeters",
                size.Width,
                size.Height);
        }

        // -------------------------------------------------------
        // INTERNAL POINT TRANSFORMATION
        // -------------------------------------------------------

        private (double X, double Y) TransformPoint(
            double x,
            double y,
            DxfInsertData insert)
        {
            // Remove block base point.
            double localX = x - insert.BasePointX;
            double localY = y - insert.BasePointY;

            // Apply scale.
            double scaledX = localX * insert.ScaleX;
            double scaledY = localY * insert.ScaleY;

            // Apply rotation.
            double radians = insert.Rotation * Math.PI / 180.0;

            double rotatedX =
                scaledX * Math.Cos(radians) -
                scaledY * Math.Sin(radians);

            double rotatedY =
                scaledX * Math.Sin(radians) +
                scaledY * Math.Cos(radians);

            // Apply insertion position.
            return (
                insert.X + rotatedX,
                insert.Y + rotatedY
            );
        }

        // Apply a parent INSERT transformation after a child
        // INSERT transformation.
        private (double X, double Y) TransformNestedPoint(
            double x,
            double y,
            DxfInsertData child,
            DxfInsertData parent)
        {
            var childPoint = TransformPoint(x, y, child);

            return TransformPoint(
                childPoint.X,
                childPoint.Y,
                parent);
        }

        // -------------------------------------------------------
        // READ BLOCK CONTENT
        // -------------------------------------------------------

        private DxfPreviewData ReadBlockContent(
            netDxf.Blocks.Block block,
            HashSet<string>? visitedBlocks = null)
        {
            var content = new DxfPreviewData();

            visitedBlocks ??= new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            // Avoid infinite recursion if blocks reference
            // each other.
            if (!visitedBlocks.Add(block.Name))
                return content;

            // LINE
            foreach (var line in block.Entities.OfType<Line>())
            {
                content.Lines.Add(new DxfLineData
                {
                    StartX = line.StartPoint.X,
                    StartY = line.StartPoint.Y,
                    EndX = line.EndPoint.X,
                    EndY = line.EndPoint.Y
                });
            }

            // CIRCLE
            foreach (var circle in block.Entities.OfType<Circle>())
            {
                content.Circles.Add(new DxfCircleData
                {
                    CenterX = circle.Center.X,
                    CenterY = circle.Center.Y,
                    Radius = circle.Radius
                });
            }

            // ARC
            foreach (var arc in block.Entities.OfType<Arc>())
            {
                content.Arcs.Add(new DxfArcData
                {
                    CenterX = arc.Center.X,
                    CenterY = arc.Center.Y,
                    Radius = arc.Radius,
                    StartAngle = arc.StartAngle,
                    EndAngle = arc.EndAngle
                });
            }

            // POLYLINE2D
            foreach (var polyline in
                     block.Entities.OfType<Polyline2D>())
            {
                var polylineData = new DxfPolylineData
                {
                    IsClosed = polyline.IsClosed
                };

                foreach (var vertex in polyline.Vertexes)
                {
                    polylineData.Points.Add(
                        new DxfPolylinePoint
                        {
                            X = vertex.Position.X,
                            Y = vertex.Position.Y,
                            Bulge = vertex.Bulge
                        });
                }

                content.Polylines.Add(polylineData);
            }

            // TEXT
            foreach (var text in block.Entities.OfType<Text>())
            {
                content.Texts.Add(new DxfTextData
                {
                    Value = text.Value,
                    X = text.Position.X,
                    Y = text.Position.Y,
                    Height = text.Height,
                    Rotation = text.Rotation
                });
            }

            // MTEXT
            foreach (var mtext in block.Entities.OfType<MText>())
            {
                content.MTexts.Add(new DxfMTextData
                {
                    Value = mtext.Value,
                    X = mtext.Position.X,
                    Y = mtext.Position.Y,
                    Height = mtext.Height,
                    Rotation = mtext.Rotation
                });
            }

            // NESTED INSERT
            foreach (var nestedInsert in
                     block.Entities.OfType<Insert>())
            {
                var nestedData = new DxfInsertData
                {
                    BlockName = nestedInsert.Block.Name,

                    X = nestedInsert.Position.X,
                    Y = nestedInsert.Position.Y,

                    BasePointX = nestedInsert.Block.Origin.X,
                    BasePointY = nestedInsert.Block.Origin.Y,

                    ScaleX = nestedInsert.Scale.X,
                    ScaleY = nestedInsert.Scale.Y,

                    Rotation = nestedInsert.Rotation,

                    BlockContent = ReadBlockContent(
                        nestedInsert.Block,
                        new HashSet<string>(
                            visitedBlocks,
                            StringComparer.OrdinalIgnoreCase))
                };

                content.Inserts.Add(nestedData);
            }

            return content;
        }

        // -------------------------------------------------------
        // READ PREVIEW DATA FROM DXF FILE
        // -------------------------------------------------------

        public DxfPreviewData ReadPreviewData(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    "DXF file not found.", filePath);

            DxfDocument document = DxfDocument.Load(filePath);

            var preview = new DxfPreviewData();

            // LINE
            foreach (var line in document.Entities.Lines)
            {
                preview.Lines.Add(new DxfLineData
                {
                    StartX = line.StartPoint.X,
                    StartY = line.StartPoint.Y,
                    EndX = line.EndPoint.X,
                    EndY = line.EndPoint.Y
                });
            }

            // CIRCLE
            foreach (var circle in document.Entities.Circles)
            {
                preview.Circles.Add(new DxfCircleData
                {
                    CenterX = circle.Center.X,
                    CenterY = circle.Center.Y,
                    Radius = circle.Radius
                });
            }

            // ARC
            foreach (var arc in document.Entities.Arcs)
            {
                preview.Arcs.Add(new DxfArcData
                {
                    CenterX = arc.Center.X,
                    CenterY = arc.Center.Y,
                    Radius = arc.Radius,
                    StartAngle = arc.StartAngle,
                    EndAngle = arc.EndAngle
                });
            }

            // POLYLINE2D
            foreach (var polyline in document.Entities.Polylines2D)
            {
                var polylineData = new DxfPolylineData
                {
                    IsClosed = polyline.IsClosed
                };

                foreach (var vertex in polyline.Vertexes)
                {
                    polylineData.Points.Add(
                        new DxfPolylinePoint
                        {
                            X = vertex.Position.X,
                            Y = vertex.Position.Y,
                            Bulge = vertex.Bulge
                        });
                }

                preview.Polylines.Add(polylineData);
            }

            // TEXT
            foreach (var text in document.Entities.Texts)
            {
                preview.Texts.Add(new DxfTextData
                {
                    Value = text.Value,
                    X = text.Position.X,
                    Y = text.Position.Y,
                    Height = text.Height,
                    Rotation = text.Rotation
                });
            }

            // MTEXT
            foreach (var mtext in document.Entities.MTexts)
            {
                preview.MTexts.Add(new DxfMTextData
                {
                    Value = mtext.Value,
                    X = mtext.Position.X,
                    Y = mtext.Position.Y,
                    Height = mtext.Height,
                    Rotation = mtext.Rotation
                });
            }

            // INSERT
            foreach (var insert in document.Entities.Inserts)
            {
                preview.Inserts.Add(new DxfInsertData
                {
                    BlockName = insert.Block.Name,

                    X = insert.Position.X,
                    Y = insert.Position.Y,

                    BasePointX = insert.Block.Origin.X,
                    BasePointY = insert.Block.Origin.Y,

                    ScaleX = insert.Scale.X,
                    ScaleY = insert.Scale.Y,

                    Rotation = insert.Rotation,

                    BlockContent = ReadBlockContent(insert.Block)
                });
            }

            return preview;
        }

        // -------------------------------------------------------
        // CALCULATE BLOCK BOUNDS
        // -------------------------------------------------------

        private void IncludeInsertBounds(
            DxfInsertData insert,
            DxfBoundingBox box)
        {
            DxfPreviewData block = insert.BlockContent;

            void Include(double x, double y)
            {
                var point = TransformPoint(x, y, insert);

                box.Include(point.X, point.Y);
            }

            // LINE
            foreach (var line in block.Lines)
            {
                Include(line.StartX, line.StartY);
                Include(line.EndX, line.EndY);
            }

            // CIRCLE
            foreach (var circle in block.Circles)
            {
                for (int angle = 0; angle < 360; angle += 5)
                {
                    double radians = angle * Math.PI / 180.0;

                    Include(
                        circle.CenterX +
                        circle.Radius * Math.Cos(radians),

                        circle.CenterY +
                        circle.Radius * Math.Sin(radians));
                }
            }

            // ARC
            foreach (var arc in block.Arcs)
            {
                double start = arc.StartAngle;
                double end = arc.EndAngle;

                if (end < start)
                    end += 360;

                for (double angle = start;
                     angle <= end;
                     angle += 5)
                {
                    double radians = angle * Math.PI / 180.0;

                    Include(
                        arc.CenterX +
                        arc.Radius * Math.Cos(radians),

                        arc.CenterY +
                        arc.Radius * Math.Sin(radians));
                }

                double endRadians = end * Math.PI / 180.0;

                Include(
                    arc.CenterX +
                    arc.Radius * Math.Cos(endRadians),

                    arc.CenterY +
                    arc.Radius * Math.Sin(endRadians));
            }

            // POLYLINE
            foreach (var polyline in block.Polylines)
            {
                foreach (var point in polyline.Points)
                {
                    Include(point.X, point.Y);
                }
            }

            // TEXT
            foreach (var text in block.Texts)
            {
                Include(text.X, text.Y);
            }

            // MTEXT
            foreach (var text in block.MTexts)
            {
                Include(text.X, text.Y);
            }

            // NESTED INSERT
            foreach (var nestedInsert in block.Inserts)
            {
                IncludeNestedInsertBounds(
                    nestedInsert,
                    insert,
                    box);
            }
        }

        private void IncludeNestedInsertBounds(
            DxfInsertData child,
            DxfInsertData parent,
            DxfBoundingBox box)
        {
            DxfPreviewData block = child.BlockContent;

            void Include(double x, double y)
            {
                var point = TransformNestedPoint(
                    x, y, child, parent);

                box.Include(point.X, point.Y);
            }

            foreach (var line in block.Lines)
            {
                Include(line.StartX, line.StartY);
                Include(line.EndX, line.EndY);
            }

            foreach (var circle in block.Circles)
            {
                for (int angle = 0; angle < 360; angle += 5)
                {
                    double radians = angle * Math.PI / 180.0;

                    Include(
                        circle.CenterX +
                        circle.Radius * Math.Cos(radians),

                        circle.CenterY +
                        circle.Radius * Math.Sin(radians));
                }
            }

            foreach (var arc in block.Arcs)
            {
                double start = arc.StartAngle;
                double end = arc.EndAngle;

                if (end < start)
                    end += 360;

                for (double angle = start;
                     angle <= end;
                     angle += 5)
                {
                    double radians = angle * Math.PI / 180.0;

                    Include(
                        arc.CenterX +
                        arc.Radius * Math.Cos(radians),

                        arc.CenterY +
                        arc.Radius * Math.Sin(radians));
                }
            }

            foreach (var polyline in block.Polylines)
            {
                foreach (var point in polyline.Points)
                {
                    Include(point.X, point.Y);
                }
            }

            foreach (var text in block.Texts)
            {
                Include(text.X, text.Y);
            }

            foreach (var text in block.MTexts)
            {
                Include(text.X, text.Y);
            }

            // Further nested INSERT levels.
            foreach (var nested in block.Inserts)
            {
                IncludeNestedInsertBounds(
                    nested,
                    parent,
                    box);
            }
        }

        // -------------------------------------------------------
        // READ COMPLETE SYMBOL BOUNDS
        // -------------------------------------------------------

        public DxfBoundingBox ReadBasicSymbolBounds(
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) ||
                !File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "DXF file not found.", filePath);
            }

            DxfDocument document = DxfDocument.Load(filePath);

            var box = new DxfBoundingBox();

            // LINE
            foreach (var line in document.Entities.Lines)
            {
                box.Include(
                    line.StartPoint.X,
                    line.StartPoint.Y);

                box.Include(
                    line.EndPoint.X,
                    line.EndPoint.Y);
            }

            // CIRCLE
            foreach (var circle in document.Entities.Circles)
            {
                box.Include(
                    circle.Center.X - circle.Radius,
                    circle.Center.Y - circle.Radius);

                box.Include(
                    circle.Center.X + circle.Radius,
                    circle.Center.Y + circle.Radius);
            }

            // ARC
            foreach (var arc in document.Entities.Arcs)
            {
                double start = arc.StartAngle;
                double end = arc.EndAngle;

                if (end < start)
                    end += 360;

                for (double angle = start;
                     angle <= end;
                     angle += 5)
                {
                    double radians = angle * Math.PI / 180.0;

                    box.Include(
                        arc.Center.X +
                        arc.Radius * Math.Cos(radians),

                        arc.Center.Y +
                        arc.Radius * Math.Sin(radians));
                }

                double endRadians = end * Math.PI / 180.0;

                box.Include(
                    arc.Center.X +
                    arc.Radius * Math.Cos(endRadians),

                    arc.Center.Y +
                    arc.Radius * Math.Sin(endRadians));
            }

            // POLYLINE
            foreach (var polyline in document.Entities.Polylines2D)
            {
                foreach (var vertex in polyline.Vertexes)
                {
                    box.Include(
                        vertex.Position.X,
                        vertex.Position.Y);
                }
            }

            // TEXT
            foreach (var text in document.Entities.Texts)
            {
                box.Include(
                    text.Position.X,
                    text.Position.Y);
            }

            // MTEXT
            foreach (var text in document.Entities.MTexts)
            {
                box.Include(
                    text.Position.X,
                    text.Position.Y);
            }

            // INSERT
            foreach (var insert in document.Entities.Inserts)
            {
                var insertData = new DxfInsertData
                {
                    BlockName = insert.Block.Name,

                    X = insert.Position.X,
                    Y = insert.Position.Y,

                    BasePointX = insert.Block.Origin.X,
                    BasePointY = insert.Block.Origin.Y,

                    ScaleX = insert.Scale.X,
                    ScaleY = insert.Scale.Y,

                    Rotation = insert.Rotation,

                    BlockContent = ReadBlockContent(insert.Block)
                };

                IncludeInsertBounds(insertData, box);
            }

            return box;
        }

        // -------------------------------------------------------
        // UNIT AND SIZE DETAILS
        // -------------------------------------------------------

        public string GetUnitAndSizeDetails(string filePath)
        {
            string units = ReadUnits(filePath);

            var size = ReadSymbolSizeInMillimetres(filePath);

            return
                $"File: {Path.GetFileName(filePath)}\n" +
                $"Units: {units}\n" +
                $"Width: {size.Width:F2} mm\n" +
                $"Height: {size.Height:F2} mm";
        }
    }
}