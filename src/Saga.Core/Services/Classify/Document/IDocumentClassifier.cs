using Saga.Core.Ingest;
using Saga.Model;

namespace Saga.Core.Services.Classify.Document
{
    /// <summary>
    /// Klasyfikator dokumentu: rozpoznaje, czy zbiór bloków to akt prawny i jakiego rodzaju (wg ZTP).
    /// Nie ocenia normatywności ani nie decyduje o parsowaniu — zwraca raport dla wywołującego.
    /// </summary>
    public interface IDocumentClassifier
    {
        DocumentClassificationResult Classify(IReadOnlyList<DocumentBlock> blocks);
    }
}
