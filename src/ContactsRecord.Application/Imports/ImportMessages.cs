namespace ContactsRecord.Application.Imports;

public static class ImportMessages
{
    public const string NotFound = "Importação não encontrada.";
    public const string QueueUnavailable =
        "Não foi possível enfileirar a importação. Tente novamente em instantes.";
    public const string UnexpectedFailure = "Erro inesperado ao processar a importação.";

    public static string DuplicateInFile(string phone, int firstLine) =>
        $"Telefone {phone} repetido no arquivo (já informado na linha {firstLine}).";

    public static string DuplicateOfContact(string phone, string name, bool isActive) =>
        $"Telefone {phone} já cadastrado para o contato \"{name}\" ({(isActive ? "ativo" : "inativo")}).";

    public static string DuplicateOfUnknownContact(string phone) => $"Telefone {phone} já cadastrado.";
}
