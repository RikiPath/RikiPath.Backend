namespace RikiPath.Application.Responses.Auth
{
    public class FirebaseVerifiedUserResponse
    {
        public string Uid { get; }
        public string PhoneNumber { get; }

        public FirebaseVerifiedUserResponse(string uid, string phoneNumber)
        {
            Uid = uid;
            PhoneNumber = phoneNumber;
        }
    }
}