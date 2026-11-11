namespace SmartParking.Application.Common.Exceptions;

public sealed class ImageValidationException(string message) : Exception(message);

public sealed class ImageConfigurationException(string message = "Cloudinary is not configured correctly. Set the CLOUDINARY_URL environment variable.")
    : Exception(message);

public sealed class ImageProviderException()
    : Exception("The image storage provider could not complete the request.");

public sealed class ImagePersistenceException()
    : Exception("Image data could not be saved or removed.");
