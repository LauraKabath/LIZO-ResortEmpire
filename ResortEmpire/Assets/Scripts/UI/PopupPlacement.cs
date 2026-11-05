using System.Collections.Generic;
using UnityEngine;

// Pure layout rule, independent of scene objects. All rects share one coordinate space.
public static class PopupPlacement
{
    // Tries above, below, right and left of the target. The first spot that leaves the target
    // visible wins; otherwise the first spot that at least stays clear of the other UI.
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

        Rect? clearOfUI = null;
        foreach (Vector2 position in preferred)
        {
            Rect spot = Fit(new Rect(position, size), area, avoided, gap);
            if (Overlaps(spot, avoided)) continue;
            if (!spot.Overlaps(target)) return spot;
            if (clearOfUI == null) clearOfUI = spot;
        }
        return clearOfUI ?? Clamp(new Rect(preferred[0], size), area);
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
