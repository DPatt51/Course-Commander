import { useState } from 'react'
import AnalyticsLab from './AnalyticsLab.jsx'
import Dashboard from './Dashboard.jsx'
import './App.css'

function App() {
  const [activeTab, setActiveTab] = useState('dashboard')

  return (
    <>
      <nav className="app-tabs">
        <button
          type="button"
          className={activeTab === 'dashboard' ? 'active' : ''}
          onClick={() => setActiveTab('dashboard')}
        >
          Dashboard
        </button>
        <button
          type="button"
          className={activeTab === 'analytics' ? 'active' : ''}
          onClick={() => setActiveTab('analytics')}
        >
          Analytics Lab
        </button>
      </nav>

      {activeTab === 'dashboard' ? <Dashboard /> : <AnalyticsLab />}
    </>
  )
}

export default App
