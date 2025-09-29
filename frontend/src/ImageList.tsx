import React, { useState, useEffect } from 'react';
import { imageAPI, ImageUpload } from './api';

const ImageList: React.FC = () => {
  const [images, setImages] = useState<ImageUpload[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    loadImages();
  }, []);

  const loadImages = async () => {
    try {
      setLoading(true);
      const imageList = await imageAPI.list();
      setImages(imageList);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to load images');
    } finally {
      setLoading(false);
    }
  };

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  if (loading) {
    return <div style={{ textAlign: 'center', padding: '20px' }}>Loading images...</div>;
  }

  if (error) {
    return <div style={{ color: 'red', textAlign: 'center', padding: '20px' }}>{error}</div>;
  }

  return (
    <div style={{ maxWidth: '800px', margin: '20px auto', padding: '20px' }}>
      <h3>My Images</h3>
      <button 
        onClick={loadImages}
        style={{ 
          marginBottom: '20px', 
          padding: '8px 16px', 
          backgroundColor: '#007bff', 
          color: 'white', 
          border: 'none',
          borderRadius: '4px',
          cursor: 'pointer'
        }}
      >
        Refresh
      </button>
      
      {images.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '40px', color: '#666' }}>
          No images uploaded yet.
        </div>
      ) : (
        <div style={{ display: 'grid', gap: '20px' }}>
          {images.map((image) => (
            <div 
              key={image.id} 
              style={{ 
                border: '1px solid #ddd', 
                borderRadius: '8px', 
                padding: '20px',
                backgroundColor: '#f9f9f9'
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div style={{ flex: 1 }}>
                  <h4 style={{ margin: '0 0 10px 0' }}>{image.originalFileName}</h4>
                  <p style={{ margin: '5px 0', color: '#666' }}>
                    Size: {formatFileSize(image.fileSizeBytes)}
                  </p>
                  <p style={{ margin: '5px 0', color: '#666' }}>
                    Uploaded: {formatDate(image.uploadedAtUtc)}
                  </p>
                  <p style={{ margin: '5px 0', color: '#666' }}>
                    Status: {image.thumbnailStatus}
                  </p>
                </div>
                {image.thumbnailUrl && (
                  <div style={{ marginLeft: '20px' }}>
                    <img 
                      src={image.thumbnailUrl} 
                      alt="Thumbnail" 
                      style={{ 
                        width: '100px', 
                        height: '100px', 
                        objectFit: 'cover',
                        borderRadius: '4px',
                        border: '1px solid #ddd'
                      }}
                    />
                    <div style={{ marginTop: '10px' }}>
                      <a 
                        href={image.thumbnailUrl} 
                        target="_blank" 
                        rel="noopener noreferrer"
                        style={{ 
                          color: '#007bff', 
                          textDecoration: 'none',
                          fontSize: '14px'
                        }}
                      >
                        Download Thumbnail
                      </a>
                    </div>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default ImageList;
