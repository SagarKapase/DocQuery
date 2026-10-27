import { useState } from 'react'
import Login from './Login'
import Documents from './Documents'
import Chat from './Chat'

export default function App() {
  const [email, setEmail] = useState(localStorage.getItem('email'))
  const [selectedDoc, setSelectedDoc] = useState(null) // null = all documents

  function handleLogin(data) {
    localStorage.setItem('token', data.token)
    localStorage.setItem('email', data.email)
    setEmail(data.email)
  }

  function logout() {
    localStorage.clear()
    setEmail(null)
  }

  if (!email) return <Login onLogin={handleLogin} />

  return (
    <div className="app">
      <header>
        <div className="brand">
          <span className="logo">DQ</span>
          <h1>DocQuery</h1>
        </div>
        <div className="user">
          <span className="avatar">{email[0].toUpperCase()}</span>
          <span className="email">{email}</span>
          <button className="btn-ghost" onClick={logout}>Logout</button>
        </div>
      </header>
      <main>
        <Documents selected={selectedDoc} onSelect={setSelectedDoc} />
        <Chat document={selectedDoc} />
      </main>
    </div>
  )
}
