import boto3
from botocore.config import Config
from datetime import datetime, timedelta

def generate_presigned_url(bucket_name, object_key, expiration=3600):
    """
    Generate a presigned URL for an S3 Object Lambda access point
    
    Parameters:
    - bucket_name: Name of the S3 Object Lambda access point
    - object_key: Key of the object to access
    - expiration: Time in seconds until the presigned URL expires (default 1 hour)
    
    Returns:
    - Presigned URL as string
    """
    # Create S3 client with signing version 4
    s3_client = boto3.client(
        's3',
        config=Config(signature_version='s3v4')
    )
    
    try:
        # Generate presigned URL
        url = s3_client.generate_presigned_url(
            'get_object',
            Params={
                'Bucket': bucket_name,
                'Key': object_key
            },
            ExpiresIn=expiration
        )
        return url
    except Exception as e:
        print(f"Error generating presigned URL: {e}")
        return None

if __name__ == "__main__":
    # Example usage
    OBJECT_LAMBDA_AP = "arn:aws:s3-object-lambda:eu-west-1:682179218046:accesspoint/objectlambdaaccesspoint55ec2237-8esptmyz4dvq"
    OBJECT_KEY = "barely-moving-louse"
    
    url = generate_presigned_url(OBJECT_LAMBDA_AP, OBJECT_KEY)
    if url:
        print(f"Presigned URL: {url}")