﻿using Robust.Shared.Serialization;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Robust.Shared.Log;
using Robust.Shared.Maths;

namespace OpenDreamShared.Dream;

public enum HorizontalAnchor {
    West,
    Left,
    Center,
    East,
    Right
}

public enum VerticalAnchor {
    South,
    Bottom,
    Center,
    North,
    Top
}

[Serializable, NetSerializable]
public sealed class ScreenLocation {
    public string? MapControl;
    public HorizontalAnchor HorizontalAnchor;
    public VerticalAnchor VerticalAnchor;
    public int X, Y;
    public int PixelOffsetX, PixelOffsetY;
    public ScreenLocation? Range;

    public int RepeatX => Range?.X - X + 1 ?? 1;
    public int RepeatY => Range?.Y - Y + 1 ?? 1;

    private static ISawmill Sawmill => Logger.GetSawmill("opendream.screen_loc_parser");

    private static string[] _keywords = [
        "CENTER",
        "WEST", "EAST", "LEFT", "RIGHT",
        "NORTH", "SOUTH", "TOP", "BOTTOM",
        "TOPLEFT", "TOPRIGHT",
        "BOTTOMLEFT", "BOTTOMRIGHT"
    ];

    public ScreenLocation(int x, int y, int pixelOffsetX, int pixelOffsetY, ScreenLocation? range = null) {
        X = x - 1;
        Y = y - 1;
        PixelOffsetX = pixelOffsetX;
        PixelOffsetY = pixelOffsetY;
        Range = range;
    }

    public ScreenLocation(int x, int y, int iconSize) {
        X = x / iconSize;
        Y = y / iconSize;

        PixelOffsetX = x % iconSize;
        PixelOffsetY = y % iconSize;
        Range = null;
    }

    public ScreenLocation(string screenLocation) {
        screenLocation = screenLocation.ToUpper(CultureInfo.InvariantCulture);

        ParseScreenLoc(screenLocation);
    }

    public Vector2 GetViewPosition(Vector2 viewOffset, ViewRange view, float tileSize, Vector2i iconSize) {
        // TODO: LEFT/RIGHT/TOP/BOTTOM need to stick to the edge of the visible map if the map's container is smaller than the map itself
        
        float x = (X + PixelOffsetX / tileSize);
        x += HorizontalAnchor switch {
            HorizontalAnchor.West or HorizontalAnchor.Left => 0,
            HorizontalAnchor.Center => view.CenterX,
            HorizontalAnchor.East => view.Width - 1,
            HorizontalAnchor.Right => view.Width - (iconSize.X / tileSize),
            _ => throw new Exception($"Invalid horizontal anchor {HorizontalAnchor}")
        };

        float y = (Y + PixelOffsetY / tileSize);
        y += VerticalAnchor switch {
            VerticalAnchor.South or VerticalAnchor.Bottom => 0,
            VerticalAnchor.Center => view.CenterY,
            VerticalAnchor.North => view.Height - 1,
            VerticalAnchor.Top => view.Height - (iconSize.Y / tileSize),
            _ => throw new Exception($"Invalid vertical anchor {VerticalAnchor}")
        };

        return viewOffset + new Vector2(x, y);
    }

    public override string ToString() {
        string mapControl = MapControl != null ? $"{MapControl}:" : string.Empty;
        string range = Range != null ? $" to {Range}" : string.Empty;

        return $"{mapControl}{HorizontalAnchor}+{X+1}:{PixelOffsetX},{VerticalAnchor}+{Y+1}:{PixelOffsetY}{range}";
    }

    public string ToCoordinates() {
        return $"{X+1}:{PixelOffsetX},{Y+1}:{PixelOffsetY}";
    }

    private void ParseScreenLoc(string screenLoc) {
        string[] rangeSplit = screenLoc.Split(" TO ");
        Range = rangeSplit.Length > 1 ? new ScreenLocation(rangeSplit[1]) : null;

        string[] coordinateSplit = rangeSplit[0].Split(",");
        if (coordinateSplit.Length is not 1 and not 2)
            throw new Exception($"Invalid screen_loc \"{screenLoc}\"");

        int mapControlSplitIndex = coordinateSplit[0].IndexOf(':');
        if (mapControlSplitIndex > 0) {
            string mapControl = rangeSplit[0].Substring(0, mapControlSplitIndex);

            if (char.IsAsciiLetter(mapControl[0]) && mapControl.IndexOfAny(['+', '-']) == -1 && !_keywords.Contains(mapControl)) {
                MapControl = mapControl;
                coordinateSplit[0] = coordinateSplit[0].Substring(mapControlSplitIndex + 1);
            }
        }

        if (coordinateSplit.Length == 1) {
            X = 0;
            Y = 0;

            (HorizontalAnchor, VerticalAnchor) = coordinateSplit[0].Trim() switch {
                "CENTER" => (HorizontalAnchor.Center, VerticalAnchor.Center),
                "NORTHWEST" => (HorizontalAnchor.West, VerticalAnchor.North),
                "TOPLEFT" => (HorizontalAnchor.Left, VerticalAnchor.Top),
                "NORTHEAST" => (HorizontalAnchor.East, VerticalAnchor.North),
                "TOPRIGHT" => (HorizontalAnchor.Right, VerticalAnchor.Top),
                "SOUTHWEST" => (HorizontalAnchor.West, VerticalAnchor.South),
                "BOTTOMLEFT" => (HorizontalAnchor.Left, VerticalAnchor.Bottom),
                "SOUTHEAST" => (HorizontalAnchor.East, VerticalAnchor.South),
                "BOTTOMRIGHT" => (HorizontalAnchor.Right, VerticalAnchor.Bottom),
                _ => throw new Exception($"Invalid screen_loc {screenLoc}")
            };

            return;
        }

        bool swappedAxis = ParseScreenLocCoordinate(coordinateSplit[0], true);
        ParseScreenLocCoordinate(coordinateSplit[1], swappedAxis);
    }

    /// <summary>
    /// Parse a string value on either side of the comma in a screen_loc
    /// </summary>
    /// <returns>Whether this set a value on an axis that doesn't match the isHorizontal arg</returns>
    private bool ParseScreenLocCoordinate(string coordinate, bool isHorizontal) {
        List<string> pieces = new();
        StringBuilder currentPiece = new();

        foreach (var c in coordinate) {
            switch (c) {
                case ' ' or '\t':
                    continue;
                case '-' or '+' when (currentPiece.Length == 0 || currentPiece[^1] != ':'):
                    // Start a new piece
                    pieces.Add(currentPiece.ToString());
                    currentPiece.Clear();
                    break;
            }

            currentPiece.Append(c);
        }

        pieces.Add(currentPiece.ToString());

        bool settingHorizontal = isHorizontal;
        bool isFirstNumber = true;
        float coordinateResult = 0.0f;
        int pixelOffsetResult = 0;
        foreach (string piece in pieces) {
            if (string.IsNullOrEmpty(piece))
                continue;

            string offsetStr;

            int pixelOffsetSeparator = piece.IndexOf(':');
            if (pixelOffsetSeparator != -1) {
                offsetStr = piece.Substring(0, pixelOffsetSeparator);
                string pixelOffsetStr = piece.Substring(pixelOffsetSeparator + 1);

                if (!int.TryParse(pixelOffsetStr, out var pixelOffset))
                    Sawmill.Error($"Invalid pixel offset {pixelOffsetStr} in {coordinate}");

                pixelOffsetResult += pixelOffset;
            } else {
                offsetStr = piece;
            }

            switch (offsetStr) {
                case "CENTER":
                    if (settingHorizontal)
                        HorizontalAnchor = HorizontalAnchor.Center;
                    else
                        VerticalAnchor = VerticalAnchor.Center;

                    break;
                case "WEST":
                    // Yes, this sets the horizontal anchor regardless of the isHorizontal arg.
                    // Every macro sets their respective axis regardless of which coordinate it's in
                    HorizontalAnchor = HorizontalAnchor.West;
                    settingHorizontal = true;
                    break;
                case "EAST":
                    HorizontalAnchor = HorizontalAnchor.East;
                    settingHorizontal = true;
                    break;
                case "NORTH":
                    VerticalAnchor = VerticalAnchor.North;
                    settingHorizontal = false;
                    break;
                case "SOUTH":
                    VerticalAnchor = VerticalAnchor.South;
                    settingHorizontal = false;
                    break;
                case "LEFT":
                    HorizontalAnchor = HorizontalAnchor.Left;
                    settingHorizontal = true;
                    break;
                case "RIGHT":
                    HorizontalAnchor = HorizontalAnchor.Right;
                    settingHorizontal = true;
                    break;
                case "TOP":
                    VerticalAnchor = VerticalAnchor.Top;
                    settingHorizontal = false;
                    break;
                case "BOTTOM":
                    VerticalAnchor = VerticalAnchor.Bottom;
                    settingHorizontal = false;
                    break;

                // A normal number
                default:
                    if (!float.TryParse(offsetStr, out var offset))
                        Sawmill.Error($"Invalid offset {offsetStr} in {coordinate}");

                    coordinateResult += offset;
                    if (isFirstNumber) // Deal with us being 0-indexed while screen_loc is 1-indexed
                        coordinateResult -= 1.0f;

                    break;
            }

            isFirstNumber = false;
        }

        // Convert decimal numbers to a pixel offset
        double fractionalOffset = coordinateResult - Math.Floor(coordinateResult);
        pixelOffsetResult += (int)(32 * fractionalOffset);

        if (settingHorizontal) {
            X = (int)coordinateResult;
            PixelOffsetX = pixelOffsetResult;
        } else {
            Y = (int)coordinateResult;
            PixelOffsetY = pixelOffsetResult;
        }

        return settingHorizontal != isHorizontal;
    }
}
