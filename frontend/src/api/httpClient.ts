import axios from 'axios';

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5028';

export const httpClient = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});



