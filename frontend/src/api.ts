import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5001';

const api = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true, // For cookie authentication
});

export interface User {
  userId: string;
  email: string;
}

export interface ImageUpload {
  id: string;
  originalFileName: string;
  fileSizeBytes: number;
  uploadedAtUtc: string;
  thumbnailStatus: 'Pending' | 'Completed';
  thumbnailUrl?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export const authAPI = {
  login: async (credentials: LoginRequest) => {
    const response = await api.post('/api/Account/login', credentials);
    return response.data;
  },

  register: async (userData: RegisterRequest) => {
    const response = await api.post('/api/Account/register', userData);
    return response.data;
  },

  logout: async () => {
    const response = await api.post('/api/Account/logout');
    return response.data;
  },

  getCurrentUser: async (): Promise<User> => {
    const response = await api.get('/api/Account/me');
    return response.data as User;
  },

  healthCheck: async () => {
    const response = await api.get('/api/Account/healthcheck');
    return response.data;
  }
};

export const imageAPI = {
  list: async (): Promise<ImageUpload[]> => {
    const response = await api.get('/api/Image');
    return response.data as ImageUpload[];
  },

  upload: async (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    
    const response = await api.post('/api/Upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  },

  getThumbnailUrl: async (imageId: string) => {
    const response = await api.get(`/api/Image/${imageId}/thumbnail`);
    return response.data;
  }
};

export default api;
