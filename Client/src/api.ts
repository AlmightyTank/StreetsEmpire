export type Activity = {
  id: number
  action: string
  summary: string
  turnsSpent: number
  cashDelta: number
  bankDelta: number
  createdAtUtc: string
}

export type StoreItem = {
  key: string
  name: string
  category: string
  price: number
  description: string
}

export type Dashboard = {
  playerId: string
  name: string
  city: string
  cash: number
  bankCash: number
  netWorth: number
  rank: number
  turns: number
  maxTurns: number
  turnsPerTick: number
  turnTickMinutes: number
  secondsUntilNextTurnTick: number
  pimps: number
  hoes: number
  thugs: number
  hoeCutPercent: number
  hoeHappiness: number
  thugHappiness: number
  condoms: number
  beer: number
  weapons: number
  weed: number
  coke: number
  weedSellPrice: number
  cokeSellPrice: number
  store: StoreItem[]
  recentActivity: Activity[]
}

export type LeaderboardEntry = {
  rank: number
  playerName: string
  city: string
  netWorth: number
  cash: number
  bankCash: number
  pimps: number
  hoes: number
  thugs: number
}

export type ActionResult = {
  summary: string
  turnsRemaining: number
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    ...options,
  })

  if (!response.ok) {
    let message = response.status === 401 ? 'Unauthorized' : `Request failed (${response.status})`
    try {
      const body = await response.json()
      if (body?.error) message = body.error
    } catch { /* empty */ }
    throw new Error(message)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  register: (username: string, password: string, playerName: string) =>
    request('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, password, playerName }),
    }),
  login: (username: string, password: string) =>
    request('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    }),
  logout: () => request('/api/auth/logout', { method: 'POST' }),
  dashboard: () => request<Dashboard>('/api/game/dashboard'),
  leaderboard: () => request<LeaderboardEntry[]>('/api/game/leaderboard'),
  workStreet: (turns: number) => request<ActionResult>('/api/game/street', {
    method: 'POST',
    body: JSON.stringify({ turns }),
  }),
  produce: (product: 'weed' | 'coke', turns: number) => request<ActionResult>('/api/game/production', {
    method: 'POST',
    body: JSON.stringify({ product, turns }),
  }),
  sellProduct: (product: 'weed' | 'coke', quantity: number) => request<ActionResult>('/api/game/product/sell', {
    method: 'POST',
    body: JSON.stringify({ product, quantity }),
  }),
  buyStoreItem: (itemKey: string, quantity: number) => request<ActionResult>('/api/game/store/buy', {
    method: 'POST',
    body: JSON.stringify({ itemKey, quantity }),
  }),
  deposit: (amount: number) => request<ActionResult>('/api/game/bank/deposit', {
    method: 'POST',
    body: JSON.stringify({ amount }),
  }),
  withdraw: (amount: number) => request<ActionResult>('/api/game/bank/withdraw', {
    method: 'POST',
    body: JSON.stringify({ amount }),
  }),
  setHoeCut: (hoeCutPercent: number) => request<ActionResult>('/api/game/crew/settings', {
    method: 'PUT',
    body: JSON.stringify({ hoeCutPercent }),
  }),
}
