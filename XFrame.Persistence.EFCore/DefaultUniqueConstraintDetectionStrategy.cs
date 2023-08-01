namespace XFrame.Persistence.EFCore
{
    internal class DefaultUniqueConstraintDetectionStrategy : IUniqueConstraintDetectionStrategy
    {
        public bool IsUniqueConstraintViolation(Exception exception)
        {
            return exception.InnerException?.Message?.IndexOf("unique", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}