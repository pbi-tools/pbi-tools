namespace PbiTools.Serialization
{
    /// <summary>
    /// Transforms a Power BI package part, that has been converted to <see cref="T"/>, into (Serialize) and back from (Deserialize) the <c>PbixProj</c> file format.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    // ReSharper disable once InconsistentNaming
    public interface IPowerBIPartSerializer<T>
    {
        void Serialize(T content);
        bool TryDeserialize(out T part);
    }
}