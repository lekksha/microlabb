namespace RtuItLab.Infrastructure.MassTransit
{
    public class BaseResponseMassTransit
    {
        public bool IsSuccess { get; set; }
        public string Error { get; set; }
    }
}
