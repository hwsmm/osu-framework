#include "LinkedList.h"
#include <stdlib.h>

void AddNode(Node **head, void *pointer)
{
    Node *new = (Node*)malloc(sizeof(Node));
    if (new == NULL)
        return;

    new->pointer = pointer;
    new->next = NULL;
    new->prev = NULL;

    if (*head == NULL)
    {
        *head = new;
    }
    else
    {
        new->next = *head;
        (*head)->prev = new;
        *head = new;
    }
}

void AddNodeAfter(Node **tail, void *pointer)
{
    Node *new = (Node *)malloc(sizeof(Node));
    if (new == NULL)
        return;

    new->pointer = pointer;
    new->next = NULL;
    new->prev = NULL;

    if (*tail == NULL)
    {
        *tail = new;
    }
    else
    {
        new->prev = *tail;
        (*tail)->next = new;
        *tail = new;
    }
}

int RemoveNode(Node **head, Node *node)
{
    if (head == NULL || *head == NULL || node == NULL)
        return -1;

    if (node == *head)
    {
        *head = node->next;
        if (*head != NULL)
            (*head)->prev = NULL;
    }
    else
    {
        if (node->prev != NULL)
            node->prev->next = node->next;

        if (node->next != NULL)
            node->next->prev = node->prev;
    }

    free(node);
    return 1;
}

void FreeList(Node *first)
{
    ITER_LINKED(first, node, free(node));
}
