from langchain_groq import ChatGroq
from app.settings import settings


def get_llm() -> ChatGroq:
    return ChatGroq(
        api_key=settings.groq_api_key,
        model=settings.groq_model,
        temperature=0,
    )
