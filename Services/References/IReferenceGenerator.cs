namespace KorridorX.Services.References;

public interface IReferenceGenerator
{
    string GenerateTransferReference();
    string GenerateCollectionReference();
    string GeneratePayoutReference();
    string GenerateBusinessBatchReference();
    string GenerateSupportTicketReference();
    string GenerateDisputeReference();
    string GenerateInvestigationReference();
}