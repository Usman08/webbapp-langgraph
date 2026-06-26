from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    groq_api_key: str
    groq_model: str = "openai/gpt-oss-120b"
    engine_token: str
    backend_url: str = "http://localhost:8080"

    class Config:
        env_file = ".env"
        case_sensitive = False


settings = Settings()  # type: ignore[call-arg]
