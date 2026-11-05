using System.Collections.Generic;
using UnityEngine;

// Pure layout rule, independent of scene objects. All rects share one coordinate space.
public static class PopupPlacement
{
    // Tries above, below, right and left of the target and takes the spot with the most room around
    // it, so the popup can follow a panning camera for as long as possible. Covering the target is
    // the last resort, used only when no other spot stays clear of the other UI.
    public static Rect Place(Vector2 size, Rect target, Rect area, IReadOnlyList<Rect> avoided, float gap)
    {
        float centeredX = target.center.x - size.x * 0.5f;
        float centeredY = target.center.y - size.y * 0.5f;
        Vector2[] preferred =
        {
            new Vector2(centeredX, target.yMax + gap),
            new Vector2(centeredX, target.yMin - gap - size.y),
            new Vector2(target.xMax + gap, centeredY),
            new Vector2(target.xMin - gap - size.x, centeredY)
        };

        Rect? best = null;
        Rect? clearOfUI = null;
        float bestRoom = float.MinValue;
        foreach (Vector2 position in preferred)
        {
            Rect spot = Fit(new Rect(position, size), area, avoided, gap);
            if (Overlaps(spot, avoided)) continue;
            if (spot.Overlaps(target))
            {
                if (clearOfUI == null) clearOfUI = spot;
                continue;
            }
            float room = RoomAround(spot, area, avoided);
            if (room <= bestRoom) continue;
            bestRoom = room;
            best = spot;
        }
        return best ?? clearOfUI ?? Clamp(new Rect(preferred[0], size), area);
    }

    // The shortest distance from the spot to the area's edges and the other UI.
    private static float RoomAround(Rect spot, Rect area, IReadOnlyList<Rect> avoided)
    {
        float room = Mathf.Min(spot.xMin - area.xMin, area.xMax - spot.xMax,
            spot.yMin - area.yMin, area.yMax - spot.yMax);
        foreach (Rect ui in avoided)
        {
            float apartX = Mathf.Max(ui.xMin - spot.xMax, spot.xMin - ui.xMax);
            float apartY = Mathf.Max(ui.yMin - spot.yMax, spot.yMin - ui.yMax);
            room = Mathf.Min(room, Mathf.Max(apartX, apartY));
        }
        return room;
    }

    // Whether a spot is inside the area, allowing for rounding, and clear of the other UI.
    public static bool Fits(Rect spot, Rect area, IReadOnlyList<Rect> avoided)
    {
        const float tolerance = 0.5f;
        return spot.xMin >= area.xMin - tolerance && spot.xMax <= area.xMax + tolerance &&
            spot.yMin >= area.yMin - tolerance && spot.yMax <= area.yMax + tolerance &&
            !Overlaps(spot, avoided);
    }

    private static Rect Fit(Rect spot, Rect area, IReadOnlyList<Rect> avoided, float gap)
    {
        spot = Clamp(spot, area);
        // Two passes, because moving away from one panel can push the popup into another.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Rect ui in avoided)
                if (spot.Overlaps(ui)) spot = MoveOut(spot, ui, area, gap);
        }
        return spot;
    }

    // The smallest move that clears the panel while staying inside the area.
    private static Rect MoveOut(Rect spot, Rect ui, Rect area, float gap)
    {
        Vector2[] moves =
        {
            new Vector2(ui.xMin - gap - spot.xMax, 0),
            new Vector2(ui.xMax + gap - spot.xMin, 0),
            new Vector2(0, ui.yMin - gap - spot.yMax),
            new Vector2(0, ui.yMax + gap - spot.yMin)
        };

        Rect best = spot;
        float bestDistance = float.MaxValue;
        foreach (Vector2 move in moves)
        {
            var moved = new Rect(spot.position + move, spot.size);
            if (moved.xMin < area.xMin || moved.xMax > area.xMax ||
                moved.yMin < area.yMin || moved.yMax > area.yMax) continue;
            if (move.sqrMagnitude >= bestDistance) continue;
            bestDistance = move.sqrMagnitude;
            best = moved;
        }
        return best;
    }

    private static Rect Clamp(Rect spot, Rect area)
    {
        spot.x = Mathf.Clamp(spot.x, area.xMin, Mathf.Max(area.xMin, area.xMax - spot.width));
        spot.y = Mathf.Clamp(spot.y, area.yMin, Mathf.Max(area.yMin, area.yMax - spot.height));
        return spot;
    }

    private static bool Overlaps(Rect spot, IReadOnlyList<Rect> avoided)
    {
        foreach (Rect ui in avoided)
            if (spot.Overlaps(ui)) return true;
        return false;
    }
}
