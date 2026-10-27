import { useState } from 'react'
import { login } from './api'

export default function Login({ onLogin }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isRegister, setIsRegister] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const data = await login(email, password, isRegister)
      onLogin(data)
    } catch (err) {
      setError(err.message)
    }
    setLoading(false)
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <span className="logo big">DQ</span>
        <h1>{isRegister ? 'Create your account' : 'Welcome back'}</h1>
        <p className="muted">Ask questions about your PDFs and get answers with citations.</p>

        <label>Email</label>
        <input type="email" placeholder="you@example.com" value={email} onChange={(e) => setEmail(e.target.value)} required />

        <label>Password</label>
        <input type="password" placeholder="At least 6 characters" value={password} onChange={(e) => setPassword(e.target.value)} required />

        {error && <p className="error">{error}</p>}

        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? 'Please wait...' : isRegister ? 'Create account' : 'Sign in'}
        </button>

        <p className="switch">
          {isRegister ? 'Already have an account?' : 'New to DocQuery?'}{' '}
          <a href="#" onClick={() => setIsRegister(!isRegister)}>{isRegister ? 'Sign in' : 'Create one'}</a>
        </p>
      </form>
    </div>
  )
}
