import httpx
from app.settings import settings


class DotNetClient:
    """HTTP client that calls .NET internal agent-tool endpoints with X-Engine-Token auth."""

    def __init__(self) -> None:
        self._client = httpx.AsyncClient(
            base_url=settings.backend_url,
            headers={"X-Engine-Token": settings.engine_token},
            timeout=30.0,
        )

    async def post_tool(self, path: str, payload: dict) -> dict:
        response = await self._client.post(f"/internal/tools/{path}", json=payload)
        response.raise_for_status()
        return response.json()

    async def aclose(self) -> None:
        await self._client.aclose()


dotnet_client = DotNetClient()
