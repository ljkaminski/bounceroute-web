import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderRoutes } from '../test/render'
import { LoginPage } from './LoginPage'

const signedOut = () => ({ status: 401 })
const routes = [
  { path: '/login', element: <LoginPage /> },
  { path: '/', element: <p>Home</p> },
]

afterEach(() => vi.unstubAllGlobals())

it('shows why sign-in failed', async () => {
  mockApi({
    'GET /api/auth/me': signedOut,
    'POST /api/auth/login': () => ({ status: 401, body: { title: 'Sign-in failed', detail: 'Email or password is incorrect.' } }),
  })
  renderRoutes(routes, '/login')

  await userEvent.type(await screen.findByLabelText('Email'), 'jordan@example.com')
  await userEvent.type(screen.getByLabelText('Password'), 'wrong-password')
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
})

it('goes to the app after signing in', async () => {
  const calls = mockApi({
    'GET /api/auth/me': signedOut,
    'POST /api/auth/login': () => ({
      status: 200,
      body: { id: 'u1', email: 'jordan@example.com', displayName: 'Jordan Hale', role: 'Crew', crewMemberId: 1 },
    }),
  })
  renderRoutes(routes, '/login')

  await userEvent.type(await screen.findByLabelText('Email'), 'jordan@example.com')
  await userEvent.type(screen.getByLabelText('Password'), 'bounceroute-crew')
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

  expect(await screen.findByText('Home')).toBeInTheDocument()
  expect(calls.find((call) => call.method === 'POST')?.body).toEqual({
    email: 'jordan@example.com',
    password: 'bounceroute-crew',
  })
})
