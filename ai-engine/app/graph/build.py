from langgraph.graph import StateGraph, END
from app.graph.state import WorkflowState
from app.graph import nodes


def build_graph():
    graph = StateGraph(WorkflowState)

    graph.add_node("parse_intent", nodes.parse_intent)
    graph.add_node("resolve_customer", nodes.resolve_customer)
    graph.add_node("get_history", nodes.get_history)
    graph.add_node("adjust_quantities", nodes.adjust_quantities)
    graph.add_node("validate_inventory", nodes.validate_inventory)
    graph.add_node("resolve_discount", nodes.resolve_discount)
    graph.add_node("build_draft", nodes.build_draft)

    graph.set_entry_point("parse_intent")
    graph.add_edge("parse_intent", "resolve_customer")

    def after_resolve_customer(state: WorkflowState) -> str:
        if state.get("error"):
            return END
        return "get_history"

    graph.add_conditional_edges("resolve_customer", after_resolve_customer)
    graph.add_edge("get_history", "adjust_quantities")
    graph.add_edge("adjust_quantities", "validate_inventory")
    graph.add_edge("validate_inventory", "resolve_discount")
    graph.add_edge("resolve_discount", "build_draft")
    graph.add_edge("build_draft", END)

    return graph.compile()


compiled_graph = build_graph()
