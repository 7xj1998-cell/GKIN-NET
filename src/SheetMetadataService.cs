using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace GKIN
{
    public static class SheetMetadataService
    {
        public const string SheetKey = "GKIN-SHEET";
        const string SourceLayerKey = "GKIN-SOURCE-LAYER";
        static void Write(Entity entity, Transaction tr, string key, params TypedValue[] values)
        {
            if (entity.ExtensionDictionary.IsNull) entity.CreateExtensionDictionary();
            var dictionary = (DBDictionary)tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);
            Xrecord record;
            if (dictionary.Contains(key)) record = (Xrecord)tr.GetObject(dictionary.GetAt(key), OpenMode.ForWrite);
            else { record = new Xrecord(); dictionary.SetAt(key, record); tr.AddNewlyCreatedDBObject(record, true); }
            using (var data = new ResultBuffer(values)) record.Data = data;
        }

        static TypedValue[] Read(Entity entity, Transaction tr, string key)
        {
            if (entity.ExtensionDictionary.IsNull) return null;
            var dictionary = (DBDictionary)tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead);
            if (!dictionary.Contains(key)) return null;
            var record = (Xrecord)tr.GetObject(dictionary.GetAt(key), OpenMode.ForRead);
            using (var data = record.Data) return data?.AsArray();
        }

        public static void Store(Entity frame, Transaction tr, string type, int index, string title) => Write(frame, tr, SheetKey,
            new TypedValue(1, type), new TypedValue(90, index), new TypedValue(1, title ?? ""));

        public static bool TryRead(Entity frame, Transaction tr, out string type, out int index, out string title)
        {
            type = title = null; index = 0;
            var data = Read(frame, tr, SheetKey);
            if (data == null || data.Length != 3) return false;
            type = Convert.ToString(data[0].Value); title = Convert.ToString(data[2].Value);
            return (type == "BD" || type == "TD" || type == "TN") && int.TryParse(Convert.ToString(data[1].Value), out index);
        }

        public static void HideSource(Entity source, ObjectId hiddenLayer, Transaction tr)
        {
            if (Read(source, tr, SourceLayerKey) == null) Write(source, tr, SourceLayerKey, new TypedValue(1, source.Layer));
            source.LayerId = hiddenLayer;
        }

        public static void RestoreCopyLayer(Entity copy, Transaction tr)
        {
            var data = Read(copy, tr, SourceLayerKey);
            if (data != null && data.Length > 0) copy.Layer = Convert.ToString(data[0].Value);
        }

        public static string SourceLayer(Entity entity, Transaction tr)
        {
            var data = Read(entity, tr, SourceLayerKey);
            return data != null && data.Length > 0 ? Convert.ToString(data[0].Value) : entity.Layer;
        }

        public static bool IsHiddenSource(Entity entity, Transaction tr) => entity.Layer == "GKIN-NONPLOT" && Read(entity, tr, SourceLayerKey) != null;
    }
}
