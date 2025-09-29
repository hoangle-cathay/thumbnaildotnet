import React from 'react';
import { useAuth } from './AuthContext';
import Login from './Login';
import ImageUploader from './ImageUploader';
import ImageList from './ImageList';

const App: React.FC = () => {
  const { user, logout, loading } = useAuth();

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '50px' }}>
        <h2>Loading...</h2>
      </div>
    );
  }

  if (!user) {
    return <Login />;
  }

  return (
    <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
      <header style={{ 
        backgroundColor: '#007bff', 
        color: 'white', 
        padding: '20px',
        marginBottom: '20px'
      }}>
        <div style={{ maxWidth: '1200px', margin: '0 auto', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h1 style={{ margin: 0 }}>Thumbnail Generator</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: '20px' }}>
            <span>Welcome, {user.email}</span>
            <button
              onClick={logout}
              style={{
                padding: '8px 16px',
                backgroundColor: 'transparent',
                color: 'white',
                border: '1px solid white',
                borderRadius: '4px',
                cursor: 'pointer'
              }}
            >
              Logout
            </button>
          </div>
        </div>
      </header>

      <main style={{ maxWidth: '1200px', margin: '0 auto', padding: '0 20px' }}>
        <ImageUploader />
        <ImageList />
      </main>
    </div>
  );
};

export default App;