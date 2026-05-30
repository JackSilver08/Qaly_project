Demo seed data and API snippets

This file contains sample payloads and curl commands to create a demo organization, group with 7 members, a sample chat message, a poll, and a meeting placeholder via the API.

1. Create organization

POST /api/organizations

{
"name": "Demo Org",
"code": "demo-org"
}

2. Create group (replace {orgId} with organization id)

POST /api/groups
{
"name": "Demo Group",
"description": "Group for demo",
"organizationId": "{orgId}"
}

3. Add members (call for each member)

POST /api/groups/{groupId}/members
{
"userId": "{userId}",
"role": "Member"
}

4. Send a sample chat message

POST /api/groups/{groupId}/messages
{
"content": "Hello demo team!",
"messageType": "Text"
}

5. Create a sample poll

POST /api/groups/{groupId}/polls
{
"question": "Which day works best?",
"options": ["Mon","Thu","Fri"],
"allowMultiple": false
}

6. Meeting

Meeting state in this demo is mostly client-side. Use the UI at `/groups/{groupId}/meeting` to start/stop screen share (browser support required).

Notes

- Use an authenticated admin user for API calls. The project seeder (`DataSeeder`) already seeds admin user `admin@qaly.dev` when database is empty.
- These snippets are illustrative; adapt to the actual organization and user ids in your database.
