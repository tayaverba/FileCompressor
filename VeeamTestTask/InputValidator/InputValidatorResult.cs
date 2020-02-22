namespace VeeamTestTask
{
    /// <summary>
    /// Represents result of input validation including some info
    /// </summary>
    public class InputValidatorResult
    {
        /// <summary>
        /// Resukt of validation
        /// </summary>
        public bool IsValid { get; }
        /// <summary>
        /// Error message if validation was unsuccessful
        /// </summary>
        public string ErrorMessage { get; }
        /// <summary>
        /// Creates <see cref="InputValidatorResult"/> usually is used for OK result
        /// </summary>
        public InputValidatorResult(bool isOk)
        {
            IsValid = isOk;
        }
        /// <summary>
        /// Creates <see cref="InputValidatorResult"/> usually is used for unsuccessful result
        /// </summary>
        public InputValidatorResult(bool isOk, string errorMessage) : this(isOk)
        {
            ErrorMessage = errorMessage;
        }
    }
}
