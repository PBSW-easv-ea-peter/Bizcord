# Identify failure points

The objective of this task is to identify potential failure points in your microservice.

## Scope

In (preferably) groups, review, discuss and identify failure points in your microservice. Note the failure points that you find in the directory of your microservice in the Github repository. Example:

`Service X sync call to Service Y could hang indenfinitely and block requests.`

## Task

You have to **review** your microservice and identify **potential failure points**. Below are some of the areas of your microservice that you can explore.

## Service dependencies

Find where a failure within one service could impact your service. What happens if a service is unavailable or network calls fail?

## Assuming stable network

Are you missing retries on any client calls? Do you have timeouts set?

## Data stores & external services

What happens if the database is unavailable or slow? What about external services like payment providers?

# Implement reliability measures

Having identified failure points in your microservice, you will now implement reliability measures to mitigate the identified failure points.

## Task

Based on the failure points identified in the previous task, you will now implement reliability measures to mitigate **some** of the failure points. Pick the most critical ones as a starting point.

Please consider the following points when implementing the reliability measures:
Architecture & Design

What layer of the architecture is most suitable for implementing the reliability measures?

## Policy for mitigating

What resiliency pattern is most suitable for mitigating the failure point?

- Retry
- Timeout
- Circuit Breaker
- Bulkhead
- Fallback
- Other (please specify which)

## Testing / Simulation

How will you test that the implemented reliability measures work as expected?