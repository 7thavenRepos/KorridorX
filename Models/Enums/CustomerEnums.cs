namespace KorridorX.Models.Enums;

public enum CustomerType
{
    Individual = 1,
    Business = 2
}

public enum KycStatus
{
    NotStarted = 1,
    Pending = 2,
    UnderReview = 3,
    Approved = 4,
    Rejected = 5,
    Expired = 6
}

public enum KybStatus
{
    NotStarted = 1,
    Pending = 2,
    UnderReview = 3,
    Approved = 4,
    Rejected = 5,
    Expired = 6
}

public enum KycDocumentType
{
    IdentityFront = 1,
    IdentityBack = 2,
    ProofOfAddress = 3,
    LivenessCheck = 4
}

public enum BusinessKybScope
{
    Full = 1,
    Minimal = 2
}

public enum BusinessKybDocumentType
{
    CertificateOfIncorporation = 1,
    ArticlesOfIncorporation = 2,
    BeneficialOwnershipCertificate = 3,
    IncorporationDocuments = 4,
    CacStatusReport = 5,
    ShareRegister = 6,
    BankStatement = 7,
    ProofOfBusinessAddress = 8,
    TaxDocument = 9,
    Other = 10
}

public enum BusinessOwnerDocumentSide
{
    Front = 1,
    Back = 2
}

public enum BusinessBeneficiaryType
{
    Individual = 1,
    Business = 2
}
