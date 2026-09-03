import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/api-client', () => ({
  apiCommand: vi.fn(),
  apiResult: vi.fn(),
}))

import { apiCommand, apiResult } from '@/utils/api-client'
import { githubApi, type GitHubRepository } from '@/utils/github-api'

const apiCommandMock = vi.mocked(apiCommand)
const apiResultMock = vi.mocked(apiResult)

beforeEach(() => {
  apiCommandMock.mockReset()
  apiResultMock.mockReset()
})

describe('githubApi', () => {
  it('loads the integration status for a project', () => {
    githubApi.status('project-1')

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/status')
  })

  it('loads installations for a project', () => {
    githubApi.installations('project-1')

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/installations')
  })

  it('loads the GitHub App installation URL', () => {
    githubApi.installUrl('project-1')

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/install-url')
  })

  it('completes an installation with a POST request', () => {
    githubApi.completeInstallation('project-1', 4217)

    expect(apiResultMock).toHaveBeenCalledWith(
      '/api/projects/project-1/github/installations/4217/complete',
      { method: 'POST' },
    )
  })

  it('loads repositories available to an installation', () => {
    githubApi.availableRepositories('project-1', 'installation-2')

    expect(apiResultMock).toHaveBeenCalledWith(
      '/api/projects/project-1/github/installations/installation-2/repositories',
    )
  })

  it('loads active repository connections', () => {
    githubApi.connections('project-1')

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/repositories')
  })

  it('connects a repository using only the API contract fields', () => {
    const repository: GitHubRepository = {
      id: 987,
      owner: 'qaly-team',
      name: 'qaly-web',
      fullName: 'qaly-team/qaly-web',
      defaultBranch: 'main',
      isPrivate: true,
    }

    githubApi.connectRepository('project-1', 'installation-2', repository)

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/repositories', {
      method: 'POST',
      body: JSON.stringify({
        gitHubInstallationId: 'installation-2',
        repositoryExternalId: 987,
        owner: 'qaly-team',
        name: 'qaly-web',
      }),
    })
  })

  it('disconnects a repository with apiCommand and DELETE', () => {
    githubApi.disconnectRepository('project-1', 'connection-3')

    expect(apiCommandMock).toHaveBeenCalledWith(
      '/api/projects/project-1/github/repositories/connection-3',
      { method: 'DELETE' },
    )
    expect(apiResultMock).not.toHaveBeenCalled()
  })

  it('loads development activity for a task', () => {
    githubApi.development('task-9')

    expect(apiResultMock).toHaveBeenCalledWith('/api/tasks/task-9/development')
  })

  it('loads GitHub management data for a project', () => {
    githubApi.management('project-1')

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/management')
  })

  it('synchronizes GitHub management data with POST', () => {
    githubApi.syncManagement('project-1')

    expect(apiResultMock).toHaveBeenCalledWith('/api/projects/project-1/github/management/sync', {
      method: 'POST',
    })
  })
})
