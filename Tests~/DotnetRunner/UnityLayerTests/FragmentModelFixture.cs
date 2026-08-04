using System.Collections.Generic;

namespace FragmentsUnity.Tests
{
    internal static class FragmentModelFixture
    {
        internal static FragmentItemMetadata Item(int localId, string category, string name)
        {
            return new FragmentItemMetadata
            {
                LocalId = localId,
                Category = category,
                Name = name
            };
        }

        internal static FragmentImportResult ResultWith(IEnumerable<FragmentItemMetadata> items)
        {
            var result = new FragmentImportResult();
            foreach (FragmentItemMetadata item in items)
            {
                PlaceAtLocalId(result, item);
                result.Instances.Add(new FragmentInstance { LocalId = item.LocalId });
            }
            return result;
        }

        internal static FragmentModelData LoadedDataFor(params FragmentItemMetadata[] items)
        {
            return FragmentModelAsset.Create(ResultWith(items)).Load();
        }

        internal static FragmentModel ModelWith(params FragmentItemMetadata[] items)
        {
            var model = new FragmentModel();
            model.SetAsset(FragmentModelAsset.Create(ResultWith(items)));
            return model;
        }

        private static void PlaceAtLocalId(FragmentImportResult result, FragmentItemMetadata item)
        {
            while (result.Items.Count <= item.LocalId)
            {
                result.Items.Add(null);
            }
            result.Items[item.LocalId] = item;
        }
    }
}
