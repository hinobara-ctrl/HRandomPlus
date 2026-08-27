using HRandomPlus.Beatmaps;

namespace HRandomPlus.Validation;

public static class BeatmapValidator
{
    public static void ValidateTransformation(OsuBeatmapDocument original, OsuBeatmapDocument reparsed)
    {
        if (original.Mode != 3 || reparsed.Mode != 3 || original.Keys != reparsed.Keys)
            throw new InvalidDataException("El modo o keymode cambió durante la transformación.");
        if (original.HitObjects.Count != reparsed.HitObjects.Count)
            throw new InvalidDataException("Cambió el número de HitObjects.");

        for (int i = 0; i < original.HitObjects.Count; i++)
        {
            ManiaHitObject before = original.HitObjects[i];
            ManiaHitObject after = reparsed.HitObjects[i];
            if (before.StartTime != after.StartTime || before.EndTime != after.EndTime || before.Type != after.Type)
                throw new InvalidDataException($"Cambió tiempo, endTime o tipo del HitObject #{i + 1}.");
            if (!before.NonPositionFieldsEqual(after))
                throw new InvalidDataException($"Se modificó un campo distinto de x en el HitObject #{i + 1}.");
            if (after.OriginalColumn < 0 || after.OriginalColumn >= reparsed.Keys)
                throw new InvalidDataException($"HitObject #{i + 1} fuera de las columnas válidas.");
        }

        ValidatePlayableStructure(reparsed.HitObjects, reparsed.Keys, assigned: false);
    }

    public static void ValidatePlayableStructure(IReadOnlyList<ManiaHitObject> objects, int keys, bool assigned)
    {
        int columnOf(ManiaHitObject h) => assigned ? h.AssignedColumn : h.OriginalColumn;
        var active = new Dictionary<int, int>();
        foreach (var group in objects.GroupBy(h => h.StartTime).OrderBy(g => g.Key))
        {
            foreach (int column in active.Where(p => p.Value <= group.Key).Select(p => p.Key).ToArray())
                active.Remove(column);

            int[] columns = group.Select(columnOf).ToArray();
            if (columns.Any(c => c < 0 || c >= keys))
                throw new InvalidDataException($"Objeto fuera de rango en {group.Key} ms.");
            if (columns.Distinct().Count() != columns.Length)
                throw new InvalidDataException($"Acorde con dos notas en la misma columna en {group.Key} ms.");
            if (columns.Any(active.ContainsKey))
                throw new InvalidDataException($"Nota incompatible con una LN activa en {group.Key} ms.");

            foreach (ManiaHitObject note in group.Where(h => h.IsLongNote))
            {
                int column = columnOf(note);
                if (!active.TryAdd(column, note.EndTime!.Value))
                    throw new InvalidDataException($"Dos LN incompatibles en la columna {column + 1}.");
            }
        }
    }
}
