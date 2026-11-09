
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SymbolLibrary.Core.Services;

namespace SymbolLibrary.Wpf
{
    public class DxfPreviewRenderer
    {
        private double _minX;
        private double _maxY;
        private double _scale;
        private double _offsetX;
        private double _offsetY;

        // -------------------------------------------------------
        // MAIN RENDER METHOD
        // -------------------------------------------------------

        public DrawingVisual Render(
            DxfPreviewData data,
            double viewportWidth = 800,
            double viewportHeight = 500)
        {
            DrawingVisual visual = new DrawingVisual();

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            // Calculate bounds for normal entities.
            IncludeDataBounds(
                data,
                ref minX,
                ref minY,
                ref maxX,
                ref maxY);

            if (double.IsInfinity(minX))
                return visual;

            double drawingWidth = maxX - minX;
            double drawingHeight = maxY - minY;

            if (drawingWidth <= 0)
                drawingWidth = 1;

            if (drawingHeight <= 0)
                drawingHeight = 1;

            double padding = 30;

            double scaleX =
                (viewportWidth - padding * 2) / drawingWidth;

            double scaleY =
                (viewportHeight - padding * 2) / drawingHeight;

            double scale = Math.Min(scaleX, scaleY);

            double offsetX =
                (viewportWidth - drawingWidth * scale) / 2;

            double offsetY =
                (viewportHeight - drawingHeight * scale) / 2;

            _minX = minX;
            _maxY = maxY;
            _scale = scale;
            _offsetX = offsetX;
            _offsetY = offsetY;

            Point ToScreen(double x, double y)
            {
                return new Point(
                    offsetX + (x - minX) * scale,
                    offsetY + (maxY - y) * scale);
            }

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(
                    Brushes.White,
                    null,
                    new Rect(0, 0, viewportWidth, viewportHeight));

                Pen pen = new Pen(Brushes.Black, 1);

                // LINE
                foreach (var line in data.Lines)
                {
                    dc.DrawLine(
                        pen,
                        ToScreen(line.StartX, line.StartY),
                        ToScreen(line.EndX, line.EndY));
                }

                // CIRCLE
                foreach (var circle in data.Circles)
                {
                    DrawCircle(
                        dc,
                        circle.CenterX,
                        circle.CenterY,
                        circle.Radius,
                        pen,
                        ToScreen);
                }

                // ARC
                foreach (var arc in data.Arcs)
                {
                    DrawArc(
                        dc,
                        arc.CenterX,
                        arc.CenterY,
                        arc.Radius,
                        arc.StartAngle,
                        arc.EndAngle,
                        pen,
                        ToScreen);
                }

                // POLYLINE
                foreach (var polyline in data.Polylines)
                {
                    DrawPolyline(
                        dc,
                        polyline,
                        pen,
                        ToScreen);
                }

                // TEXT
                foreach (var text in data.Texts)
                {
                    DrawText(
                        dc,
                        text.Value,
                        text.X,
                        text.Y,
                        text.Height,
                        text.Rotation,
                        scale,
                        ToScreen);
                }

                // MTEXT
                foreach (var text in data.MTexts)
                {
                    DrawText(
                        dc,
                        text.Value,
                        text.X,
                        text.Y,
                        text.Height,
                        text.Rotation,
                        scale,
                        ToScreen);
                }

                // INSERT BLOCKS
                foreach (var insert in data.Inserts)
                {
                    DrawBlock(
                        dc,
                        insert,
                        new List<DxfInsertData>(),
                        pen,
                        scale,
                        ToScreen);
                }
            }

            return visual;
        }

        // -------------------------------------------------------
        // CALCULATE ALL DRAWING BOUNDS
        // -------------------------------------------------------

        private static void IncludeDataBounds(
            DxfPreviewData data,
            ref double minX,
            ref double minY,
            ref double maxX,
            ref double maxY)
        {
            // LINE
            foreach (var line in data.Lines)
            {
                IncludePoint(
                    line.StartX, line.StartY,
                    ref minX, ref minY, ref maxX, ref maxY);

                IncludePoint(
                    line.EndX, line.EndY,
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            // CIRCLE
            foreach (var circle in data.Circles)
            {
                IncludeCircleBounds(
                    circle.CenterX,
                    circle.CenterY,
                    circle.Radius,
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            // ARC
            foreach (var arc in data.Arcs)
            {
                IncludeArcBounds(
                    arc.CenterX,
                    arc.CenterY,
                    arc.Radius,
                    arc.StartAngle,
                    arc.EndAngle,
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            // POLYLINE
            foreach (var polyline in data.Polylines)
            {
                List<Point> points = GetPolylinePoints(polyline);

                foreach (Point point in points)
                {
                    IncludePoint(
                        point.X, point.Y,
                        ref minX, ref minY, ref maxX, ref maxY);
                }
            }

            // TEXT
            foreach (var text in data.Texts)
            {
                IncludePoint(
                    text.X, text.Y,
                    ref minX, ref minY, ref maxX, ref maxY);

                IncludePoint(
                    text.X + text.Value.Length * text.Height * 0.6,
                    text.Y + text.Height,
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            // MTEXT
            foreach (var text in data.MTexts)
            {
                IncludePoint(
                    text.X, text.Y,
                    ref minX, ref minY, ref maxX, ref maxY);

                IncludePoint(
                    text.X + text.Value.Length * text.Height * 0.6,
                    text.Y + text.Height,
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            // INSERT
            foreach (var insert in data.Inserts)
            {
                IncludeBlockBounds(
                    insert,
                    new List<DxfInsertData>(),
                    ref minX, ref minY, ref maxX, ref maxY);
            }
        }

        // -------------------------------------------------------
        // INCLUDE BLOCK BOUNDS
        // -------------------------------------------------------


        private static void IncludeBlockBounds(
            DxfInsertData insert,
            List<DxfInsertData> parentTransforms,
            ref double minX,
            ref double minY,
            ref double maxX,
            ref double maxY)
        {
            List<DxfInsertData> transforms =
                CreateTransformChain(insert, parentTransforms);

            DxfPreviewData block = insert.BlockContent;

            // Use local variables instead of capturing ref parameters.
            double localMinX = double.PositiveInfinity;
            double localMinY = double.PositiveInfinity;
            double localMaxX = double.NegativeInfinity;
            double localMaxY = double.NegativeInfinity;

            void Include(double x, double y)
            {
                Point point = TransformPoint(x, y, transforms);

                localMinX = Math.Min(localMinX, point.X);
                localMinY = Math.Min(localMinY, point.Y);
                localMaxX = Math.Max(localMaxX, point.X);
                localMaxY = Math.Max(localMaxY, point.Y);
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
                        circle.CenterX + circle.Radius * Math.Cos(radians),
                        circle.CenterY + circle.Radius * Math.Sin(radians));
                }
            }

            // ARC
            foreach (var arc in block.Arcs)
            {
                IncludeArcUsingTransform(
                    arc.CenterX,
                    arc.CenterY,
                    arc.Radius,
                    arc.StartAngle,
                    arc.EndAngle,
                    Include);
            }

            // POLYLINE
            foreach (var polyline in block.Polylines)
            {
                foreach (Point point in GetPolylinePoints(polyline))
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
                IncludeBlockBounds(
                    nestedInsert,
                    transforms,
                    ref localMinX,
                    ref localMinY,
                    ref localMaxX,
                    ref localMaxY);
            }

            // Merge local bounds into the main drawing bounds.
            if (!double.IsInfinity(localMinX))
            {
                minX = Math.Min(minX, localMinX);
                minY = Math.Min(minY, localMinY);
                maxX = Math.Max(maxX, localMaxX);
                maxY = Math.Max(maxY, localMaxY);
            }
        }


        // -------------------------------------------------------
        // CREATE TRANSFORMATION CHAIN
        // -------------------------------------------------------

        private static List<DxfInsertData> CreateTransformChain(
            DxfInsertData insert,
            List<DxfInsertData> parentTransforms)
        {
            var transforms = new List<DxfInsertData>();

            // Child transform is applied first.
            transforms.Add(insert);

            // Then apply parent transforms.
            transforms.AddRange(parentTransforms);

            return transforms;
        }

        // -------------------------------------------------------
        // TRANSFORM BLOCK POINT
        // -------------------------------------------------------

        private static Point TransformPoint(
            double x,
            double y,
            List<DxfInsertData> transforms)
        {
            Point point = new Point(x, y);

            foreach (DxfInsertData insert in transforms)
            {
                // 1. Subtract block base point.
                double localX = point.X - insert.BasePointX;
                double localY = point.Y - insert.BasePointY;

                // 2. Apply scale.
                double scaledX = localX * insert.ScaleX;
                double scaledY = localY * insert.ScaleY;

                // 3. Apply rotation.
                double radians =
                    insert.Rotation * Math.PI / 180.0;

                double rotatedX =
                    scaledX * Math.Cos(radians) -
                    scaledY * Math.Sin(radians);

                double rotatedY =
                    scaledX * Math.Sin(radians) +
                    scaledY * Math.Cos(radians);

                // 4. Apply insertion position.
                point = new Point(
                    insert.X + rotatedX,
                    insert.Y + rotatedY);
            }

            return point;
        }

        // -------------------------------------------------------
        // DRAW BLOCK AND NESTED BLOCKS
        // -------------------------------------------------------

        private static void DrawBlock(
            DrawingContext dc,
            DxfInsertData insert,
            List<DxfInsertData> parentTransforms,
            Pen pen,
            double scale,
            Func<double, double, Point> toScreen)
        {
            List<DxfInsertData> transforms =
                CreateTransformChain(insert, parentTransforms);

            DxfPreviewData block = insert.BlockContent;

            Point Transform(double x, double y)
            {
                return TransformPoint(x, y, transforms);
            }

            // LINE
            foreach (var line in block.Lines)
            {
                Point start = Transform(
                    line.StartX, line.StartY);

                Point end = Transform(
                    line.EndX, line.EndY);

                dc.DrawLine(
                    pen,
                    toScreen(start.X, start.Y),
                    toScreen(end.X, end.Y));
            }

            // CIRCLE
            foreach (var circle in block.Circles)
            {
                DrawTransformedCircle(
                    dc,
                    circle.CenterX,
                    circle.CenterY,
                    circle.Radius,
                    pen,
                    Transform,
                    toScreen);
            }

            // ARC
            foreach (var arc in block.Arcs)
            {
                DrawTransformedArc(
                    dc,
                    arc.CenterX,
                    arc.CenterY,
                    arc.Radius,
                    arc.StartAngle,
                    arc.EndAngle,
                    pen,
                    Transform,
                    toScreen);
            }

            // POLYLINE
            foreach (var polyline in block.Polylines)
            {
                List<Point> points = GetPolylinePoints(polyline);

                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point start = Transform(
                        points[i].X, points[i].Y);

                    Point end = Transform(
                        points[i + 1].X, points[i + 1].Y);

                    dc.DrawLine(
                        pen,
                        toScreen(start.X, start.Y),
                        toScreen(end.X, end.Y));
                }
            }

            // TEXT
            foreach (var text in block.Texts)
            {
                Point position = Transform(text.X, text.Y);

                double totalScaleY = GetTotalScaleY(transforms);
                double totalRotation = GetTotalRotation(transforms);

                DrawText(
                    dc,
                    text.Value,
                    position.X,
                    position.Y,
                    text.Height * totalScaleY,
                    text.Rotation + totalRotation,
                    scale,
                    toScreen);
            }

            // MTEXT
            foreach (var text in block.MTexts)
            {
                Point position = Transform(text.X, text.Y);

                double totalScaleY = GetTotalScaleY(transforms);
                double totalRotation = GetTotalRotation(transforms);

                DrawText(
                    dc,
                    text.Value,
                    position.X,
                    position.Y,
                    text.Height * totalScaleY,
                    text.Rotation + totalRotation,
                    scale,
                    toScreen);
            }

            // RECURSIVE NESTED INSERTS
            foreach (var nestedInsert in block.Inserts)
            {
                DrawBlock(
                    dc,
                    nestedInsert,
                    transforms,
                    pen,
                    scale,
                    toScreen);
            }
        }

        // -------------------------------------------------------
        // TOTAL BLOCK SCALE AND ROTATION
        // -------------------------------------------------------

        private static double GetTotalScaleY(
            List<DxfInsertData> transforms)
        {
            double result = 1.0;

            foreach (var transform in transforms)
            {
                result *= Math.Abs(transform.ScaleY);
            }

            return result;
        }

        private static double GetTotalRotation(
            List<DxfInsertData> transforms)
        {
            double result = 0;

            foreach (var transform in transforms)
            {
                result += transform.Rotation;
            }

            return result;
        }

        // -------------------------------------------------------
        // DRAW CIRCLE
        // -------------------------------------------------------

        private static void DrawCircle(
            DrawingContext dc,
            double centerX,
            double centerY,
            double radius,
            Pen pen,
            Func<double, double, Point> toScreen)
        {
            int steps = 72;

            Point previous = toScreen(
                centerX + radius,
                centerY);

            for (int i = 1; i <= steps; i++)
            {
                double angle = 2 * Math.PI * i / steps;

                Point current = toScreen(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle));

                dc.DrawLine(pen, previous, current);

                previous = current;
            }
        }

        // -------------------------------------------------------
        // DRAW TRANSFORMED CIRCLE
        // -------------------------------------------------------

        private static void DrawTransformedCircle(
            DrawingContext dc,
            double centerX,
            double centerY,
            double radius,
            Pen pen,
            Func<double, double, Point> transform,
            Func<double, double, Point> toScreen)
        {
            int steps = 72;

            Point previous = transform(
                centerX + radius,
                centerY);

            for (int i = 1; i <= steps; i++)
            {
                double angle = 2 * Math.PI * i / steps;

                Point current = transform(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle));

                dc.DrawLine(
                    pen,
                    toScreen(previous.X, previous.Y),
                    toScreen(current.X, current.Y));

                previous = current;
            }
        }

        // -------------------------------------------------------
        // DRAW ARC
        // -------------------------------------------------------

        private static void DrawArc(
            DrawingContext dc,
            double centerX,
            double centerY,
            double radius,
            double startAngle,
            double endAngle,
            Pen pen,
            Func<double, double, Point> toScreen)
        {
            if (endAngle < startAngle)
                endAngle += 360;

            Point previous = toScreen(
                centerX + radius *
                Math.Cos(startAngle * Math.PI / 180.0),

                centerY + radius *
                Math.Sin(startAngle * Math.PI / 180.0));

            for (double angle = startAngle + 2;
                 angle < endAngle;
                 angle += 2)
            {
                double radians = angle * Math.PI / 180.0;

                Point current = toScreen(
                    centerX + radius * Math.Cos(radians),
                    centerY + radius * Math.Sin(radians));

                dc.DrawLine(pen, previous, current);

                previous = current;
            }

            Point finalPoint = toScreen(
                centerX + radius *
                Math.Cos(endAngle * Math.PI / 180.0),

                centerY + radius *
                Math.Sin(endAngle * Math.PI / 180.0));

            dc.DrawLine(pen, previous, finalPoint);
        }

        // -------------------------------------------------------
        // DRAW TRANSFORMED ARC
        // -------------------------------------------------------

        private static void DrawTransformedArc(
            DrawingContext dc,
            double centerX,
            double centerY,
            double radius,
            double startAngle,
            double endAngle,
            Pen pen,
            Func<double, double, Point> transform,
            Func<double, double, Point> toScreen)
        {
            if (endAngle < startAngle)
                endAngle += 360;

            Point previous = transform(
                centerX + radius *
                Math.Cos(startAngle * Math.PI / 180.0),

                centerY + radius *
                Math.Sin(startAngle * Math.PI / 180.0));

            for (double angle = startAngle + 2;
                 angle < endAngle;
                 angle += 2)
            {
                double radians = angle * Math.PI / 180.0;

                Point current = transform(
                    centerX + radius * Math.Cos(radians),
                    centerY + radius * Math.Sin(radians));

                dc.DrawLine(
                    pen,
                    toScreen(previous.X, previous.Y),
                    toScreen(current.X, current.Y));

                previous = current;
            }

            Point finalPoint = transform(
                centerX + radius *
                Math.Cos(endAngle * Math.PI / 180.0),

                centerY + radius *
                Math.Sin(endAngle * Math.PI / 180.0));

            dc.DrawLine(
                pen,
                toScreen(previous.X, previous.Y),
                toScreen(finalPoint.X, finalPoint.Y));
        }

        // -------------------------------------------------------
        // DRAW POLYLINE
        // -------------------------------------------------------

        private static void DrawPolyline(
            DrawingContext dc,
            DxfPolylineData polyline,
            Pen pen,
            Func<double, double, Point> toScreen)
        {
            List<Point> points = GetPolylinePoints(polyline);

            for (int i = 0; i < points.Count - 1; i++)
            {
                dc.DrawLine(
                    pen,
                    toScreen(points[i].X, points[i].Y),
                    toScreen(points[i + 1].X, points[i + 1].Y));
            }
        }

        // -------------------------------------------------------
        // GET POLYLINE POINTS INCLUDING BULGES
        // -------------------------------------------------------

        private static List<Point> GetPolylinePoints(
            DxfPolylineData polyline)
        {
            List<Point> result = new();

            int count = polyline.Points.Count;

            if (count < 2)
                return result;

            int segmentCount = polyline.IsClosed
                ? count
                : count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                var start = polyline.Points[i];
                var end = polyline.Points[(i + 1) % count];

                List<Point> segment = SampleBulgeSegment(
                    start.X,
                    start.Y,
                    end.X,
                    end.Y,
                    start.Bulge);

                if (result.Count > 0)
                    segment.RemoveAt(0);

                result.AddRange(segment);
            }

            return result;
        }

        // -------------------------------------------------------
        // SAMPLE BULGE SEGMENT
        // -------------------------------------------------------

        private static List<Point> SampleBulgeSegment(
            double x1,
            double y1,
            double x2,
            double y2,
            double bulge)
        {
            List<Point> points = new();

            double dx = x2 - x1;
            double dy = y2 - y1;

            double chord = Math.Sqrt(dx * dx + dy * dy);

            points.Add(new Point(x1, y1));

            if (Math.Abs(bulge) < 0.0000001 ||
                chord < 0.0000001)
            {
                points.Add(new Point(x2, y2));
                return points;
            }

            double sweepAngle = 4 * Math.Atan(bulge);

            double radius =
                chord * (1 + bulge * bulge) /
                (4 * Math.Abs(bulge));

            double midpointX = (x1 + x2) / 2;
            double midpointY = (y1 + y2) / 2;

            double centerOffset =
                chord * (1 - bulge * bulge) /
                (4 * bulge);

            double centerX =
                midpointX - (dy / chord) * centerOffset;

            double centerY =
                midpointY + (dx / chord) * centerOffset;

            double startAngle =
                Math.Atan2(y1 - centerY, x1 - centerX);

            int steps = Math.Max(
                2,
                (int)Math.Ceiling(
                    Math.Abs(sweepAngle) / (Math.PI / 36)));

            points.Clear();

            for (int i = 0; i <= steps; i++)
            {
                double angle =
                    startAngle + sweepAngle * i / steps;

                double x = centerX + radius * Math.Cos(angle);
                double y = centerY + radius * Math.Sin(angle);

                points.Add(new Point(x, y));
            }

            return points;
        }

        // -------------------------------------------------------
        // INCLUDE CIRCLE BOUNDS
        // -------------------------------------------------------

        private static void IncludeCircleBounds(
            double centerX,
            double centerY,
            double radius,
            ref double minX,
            ref double minY,
            ref double maxX,
            ref double maxY)
        {
            for (int angle = 0; angle < 360; angle += 5)
            {
                double radians = angle * Math.PI / 180.0;

                IncludePoint(
                    centerX + radius * Math.Cos(radians),
                    centerY + radius * Math.Sin(radians),
                    ref minX, ref minY, ref maxX, ref maxY);
            }
        }

        // -------------------------------------------------------
        // INCLUDE ARC BOUNDS
        // -------------------------------------------------------

        private static void IncludeArcBounds(
            double centerX,
            double centerY,
            double radius,
            double startAngle,
            double endAngle,
            ref double minX,
            ref double minY,
            ref double maxX,
            ref double maxY)
        {
            if (endAngle < startAngle)
                endAngle += 360;

            for (double angle = startAngle;
                 angle <= endAngle;
                 angle += 5)
            {
                double radians = angle * Math.PI / 180.0;

                IncludePoint(
                    centerX + radius * Math.Cos(radians),
                    centerY + radius * Math.Sin(radians),
                    ref minX, ref minY, ref maxX, ref maxY);
            }

            double finalRadians = endAngle * Math.PI / 180.0;

            IncludePoint(
                centerX + radius * Math.Cos(finalRadians),
                centerY + radius * Math.Sin(finalRadians),
                ref minX, ref minY, ref maxX, ref maxY);
        }

        private static void IncludeArcUsingTransform(
            double centerX,
            double centerY,
            double radius,
            double startAngle,
            double endAngle,
            Action<double, double> include)
        {
            if (endAngle < startAngle)
                endAngle += 360;

            for (double angle = startAngle;
                 angle <= endAngle;
                 angle += 5)
            {
                double radians = angle * Math.PI / 180.0;

                include(
                    centerX + radius * Math.Cos(radians),
                    centerY + radius * Math.Sin(radians));
            }

            double finalRadians = endAngle * Math.PI / 180.0;

            include(
                centerX + radius * Math.Cos(finalRadians),
                centerY + radius * Math.Sin(finalRadians));
        }

        // -------------------------------------------------------
        // DRAW TEXT AND MTEXT
        // -------------------------------------------------------

        private static void DrawText(
            DrawingContext dc,
            string value,
            double x,
            double y,
            double height,
            double rotation,
            double scale,
            Func<double, double, Point> toScreen)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            double fontSize = Math.Max(1, height * scale);

            FormattedText formattedText = new FormattedText(
                value,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                fontSize,
                Brushes.Black,
                1.0);

            Point position = toScreen(x, y);

            dc.PushTransform(
                new TranslateTransform(position.X, position.Y));

            dc.PushTransform(
                new RotateTransform(-rotation));

            dc.DrawText(
                formattedText,
                new Point(0, -fontSize));

            dc.Pop();
            dc.Pop();
        }

        // -------------------------------------------------------
        // SCREEN TO DXF COORDINATES
        // -------------------------------------------------------

        public Point ScreenToDxf(Point screenPoint)
        {
            if (_scale <= 0)
                return new Point();

            double dxfX =
                _minX + (screenPoint.X - _offsetX) / _scale;

            double dxfY =
                _maxY - (screenPoint.Y - _offsetY) / _scale;

            return new Point(dxfX, dxfY);
        }

        // -------------------------------------------------------
        // INCLUDE POINT IN BOUNDS
        // -------------------------------------------------------

        private static void IncludePoint(
            double x,
            double y,
            ref double minX,
            ref double minY,
            ref double maxX,
            ref double maxY)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
    }
}
