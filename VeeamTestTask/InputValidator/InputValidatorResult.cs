namespace VeeamTestTask
{
    public class InputValidatorResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }
        public InputValidatorResult(bool isOk)
        {
            IsValid = isOk;
        }
        public InputValidatorResult(bool isOk, string errorMessage) : this(isOk)
        {
            ErrorMessage = errorMessage;
        }
    }
}
