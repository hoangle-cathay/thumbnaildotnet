import React, { useState, useRef } from 'react';
import { imageAPI, ImageUpload } from './api';

const ImageUploader: React.FC = () => {
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (selectedFile) {
      if (selectedFile.type === 'image/png' || selectedFile.type === 'image/jpeg') {
        setFile(selectedFile);
        setError('');
      } else {
        setError('Only PNG and JPG files are allowed');
        setFile(null);
      }
    }
  };

  const handleUpload = async () => {
    if (!file) return;

    setUploading(true);
    setMessage('');
    setError('');

    try {
      const result = await imageAPI.upload(file) as any;
      setMessage(`Upload successful! Thumbnail URL: ${result.thumbnailUrl}`);
      setFile(null);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    } catch (err: any) {
      setError(err.response?.data?.error || 'Upload failed');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div style={{ maxWidth: '500px', margin: '20px auto', padding: '20px' }}>
      <h3>Upload Image</h3>
      <div style={{ marginBottom: '15px' }}>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/png,image/jpeg"
          onChange={handleFileChange}
          style={{ width: '100%', padding: '8px' }}
        />
      </div>
      {file && (
        <div style={{ marginBottom: '15px' }}>
          <p>Selected: {file.name} ({(file.size / 1024).toFixed(1)} KB)</p>
        </div>
      )}
      {error && <div style={{ color: 'red', marginBottom: '15px' }}>{error}</div>}
      {message && <div style={{ color: 'green', marginBottom: '15px' }}>{message}</div>}
      <button
        onClick={handleUpload}
        disabled={!file || uploading}
        style={{
          width: '100%',
          padding: '10px',
          backgroundColor: uploading ? '#ccc' : '#28a745',
          color: 'white',
          border: 'none',
          borderRadius: '4px',
          cursor: uploading ? 'not-allowed' : 'pointer'
        }}
      >
        {uploading ? 'Uploading...' : 'Upload'}
      </button>
    </div>
  );
};

export default ImageUploader;
