namespace KorridorX.Services.References;

public interface IReferenceGenerator
{
    string GenerateTransferReference();
    string GenerateCollectionReference();
    string GeneratePayoutReference();
}