#pragma once

typedef struct _node Node;

struct _node
{
    void *pointer;
    Node *prev;
    Node *next;
};

#define ITER_LINKED(NODE, NAME, FUNC) \
do \
{ \
	Node *NAME = NODE; \
	while (NAME != NULL) \
	{ \
		Node *__next = NAME->next; \
		FUNC; \
		NAME = __next; \
	} \
} while (0);

#define ITER_LINKED_UNBOX(NODE, TYPE, NAME, FUNC) \
ITER_LINKED(NODE, __node, \
{ \
	TYPE NAME = (TYPE)__node->pointer; \
	FUNC; \
});

void AddNode(Node **head, void *pointer);
int RemoveNode(Node **head, Node *node);
void FreeList(Node *first);
