namespace LojaLimpeza.Shared;

public static class DocumentoValidator
{
    public static string Normalizar(string? documento) =>
        new((documento ?? string.Empty).Where(character => character is >= '0' and <= '9').ToArray());

    public static bool EhValido(string? documento, string? tipoPessoa)
    {
        var digits = Normalizar(documento);
        return tipoPessoa?.ToUpperInvariant() switch
        {
            "PF" => ValidarCpf(digits),
            "PJ" => ValidarCnpj(digits),
            _ => false
        };
    }

    private static bool ValidarCpf(string cpf)
    {
        if (cpf.Length != 11 || TodosIguais(cpf))
            return false;

        var digits = cpf.Select(c => c - '0').ToArray();
        var first = CalcularDigito(cpf[..9], new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 });
        var second = CalcularDigito(cpf[..9] + first, new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 });
        return digits[9] == first && digits[10] == second;
    }

    private static bool ValidarCnpj(string cnpj)
    {
        if (cnpj.Length != 14 || TodosIguais(cnpj))
            return false;

        var first = CalcularDigito(cnpj[..12], new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });
        var second = CalcularDigito(cnpj[..12] + first, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });
        return cnpj[12] - '0' == first && cnpj[13] - '0' == second;
    }

    private static int CalcularDigito(string baseDigits, IReadOnlyList<int> weights)
    {
        var sum = baseDigits.Select((digit, index) => (digit - '0') * weights[index]).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static bool TodosIguais(string value) => value.All(character => character == value[0]);
}
